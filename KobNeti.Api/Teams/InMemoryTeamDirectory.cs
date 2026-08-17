using System.Text.RegularExpressions;
using KobNeti.Api.Staff;

namespace KobNeti.Api.Teams;

public class InMemoryTeamDirectory : ITeamDirectory
{
    private readonly List<TeamRecord> _teams = [];
    private readonly IStaffDirectory _staff;
    private readonly object _gate = new();

    public InMemoryTeamDirectory(IStaffDirectory staff) => _staff = staff;

    public Task<IReadOnlyList<TeamRecord>> ListAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            IReadOnlyList<TeamRecord> list = _teams
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .Select(Clone)
                .ToList();
            return Task.FromResult(list);
        }
    }

    public Task<TeamRecord> CreateAsync(
        string name,
        string? slug,
        string? description,
        string? productSlug,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Team name is required.");

        var normalizedSlug = string.IsNullOrWhiteSpace(slug)
            ? Slugify(name)
            : Slugify(slug);

        lock (_gate)
        {
            if (_teams.Any(t => string.Equals(t.Slug, normalizedSlug, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Team slug '{normalizedSlug}' already exists.");

            var team = new TeamRecord
            {
                Id = Guid.NewGuid(),
                Name = name.Trim(),
                Slug = normalizedSlug,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                ProductSlug = string.IsNullOrWhiteSpace(productSlug) ? null : productSlug.Trim(),
                Active = true,
                Members = []
            };
            _teams.Add(team);
            return Task.FromResult(Clone(team));
        }
    }

    public async Task<TeamRecord?> AddMemberAsync(
        Guid teamId,
        Guid staffId,
        string memberRole,
        CancellationToken ct = default)
    {
        var role = NormalizeMemberRole(memberRole);
        var staffList = await _staff.ListAsync(ct);
        var staff = staffList.FirstOrDefault(s => s.Id == staffId);
        if (staff is null)
            throw new InvalidOperationException("Staff member not found.");

        lock (_gate)
        {
            var team = _teams.FirstOrDefault(t => t.Id == teamId);
            if (team is null)
                return null;

            var members = team.Members.ToList();
            var existing = members.FirstOrDefault(m => m.StaffId == staffId);
            if (existing is not null)
            {
                existing.MemberRole = role;
            }
            else
            {
                members.Add(new TeamMemberRecord
                {
                    StaffId = staff.Id,
                    Email = staff.Email,
                    DisplayName = staff.DisplayName,
                    MemberRole = role
                });
            }

            team.Members = members;
            return Clone(team);
        }
    }

    public Task<bool> RemoveMemberAsync(Guid teamId, Guid staffId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var team = _teams.FirstOrDefault(t => t.Id == teamId);
            if (team is null)
                return Task.FromResult(false);

            var before = team.Members.Count;
            team.Members = team.Members.Where(m => m.StaffId != staffId).ToList();
            return Task.FromResult(team.Members.Count < before);
        }
    }

    private static string NormalizeMemberRole(string role)
    {
        var r = string.IsNullOrWhiteSpace(role) ? "member" : role.Trim().ToLowerInvariant();
        return r is "lead" or "member" ? r : throw new ArgumentException("Member role must be 'lead' or 'member'.");
    }

    private static string Slugify(string value)
    {
        var slug = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? $"team-{Guid.NewGuid():N}"[..12] : slug;
    }

    private static TeamRecord Clone(TeamRecord t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Slug = t.Slug,
        Description = t.Description,
        ProductSlug = t.ProductSlug,
        Active = t.Active,
        Members = t.Members.Select(m => new TeamMemberRecord
        {
            StaffId = m.StaffId,
            Email = m.Email,
            DisplayName = m.DisplayName,
            MemberRole = m.MemberRole
        }).ToList()
    };
}
