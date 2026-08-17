using System.Text.RegularExpressions;
using KobNeti.Api.Staff;
using Postgrest.Attributes;
using Postgrest.Models;
using static Postgrest.Constants;

namespace KobNeti.Api.Teams;

[Table("teams")]
public class SbTeam : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("name")] public string Name { get; set; } = string.Empty;
    [Column("slug")] public string Slug { get; set; } = string.Empty;
    [Column("description")] public string? Description { get; set; }
    [Column("product_slug")] public string? ProductSlug { get; set; }
    [Column("active")] public bool Active { get; set; } = true;
}

[Table("team_members")]
public class SbTeamMember : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("team_id")] public Guid TeamId { get; set; }
    [Column("staff_id")] public Guid StaffId { get; set; }
    [Column("member_role")] public string MemberRole { get; set; } = "member";
}

public class SupabaseTeamDirectory : ITeamDirectory
{
    private readonly Supabase.Client _client;
    private readonly InMemoryTeamDirectory _fallback;
    private readonly IStaffDirectory _staff;
    private readonly ILogger<SupabaseTeamDirectory> _logger;

    public SupabaseTeamDirectory(
        Supabase.Client client,
        InMemoryTeamDirectory fallback,
        IStaffDirectory staff,
        ILogger<SupabaseTeamDirectory> logger)
    {
        _client = client;
        _fallback = fallback;
        _staff = staff;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TeamRecord>> ListAsync(CancellationToken ct = default)
    {
        try
        {
            var teams = await _client.From<SbTeam>()
                .Filter("active", Operator.Equals, "true")
                .Order("name", Ordering.Ascending)
                .Get();

            var rows = teams.Models ?? [];
            if (rows.Count == 0)
                return await _fallback.ListAsync(ct);

            var members = await _client.From<SbTeamMember>().Get();
            var staff = await _staff.ListAsync(ct);
            var staffById = staff.ToDictionary(s => s.Id);

            return rows.Select(t => Map(t, members.Models ?? [], staffById)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Teams list failed; using in-memory.");
            return await _fallback.ListAsync(ct);
        }
    }

    public async Task<TeamRecord> CreateAsync(
        string name,
        string? slug,
        string? description,
        string? productSlug,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Team name is required.");

        var normalizedSlug = string.IsNullOrWhiteSpace(slug) ? Slugify(name) : Slugify(slug);
        try
        {
            var row = new SbTeam
            {
                Id = Guid.NewGuid(),
                Name = name.Trim(),
                Slug = normalizedSlug,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                ProductSlug = string.IsNullOrWhiteSpace(productSlug) ? null : productSlug.Trim(),
                Active = true
            };
            var inserted = await _client.From<SbTeam>().Insert(row);
            var created = (inserted.Models ?? []).FirstOrDefault() ?? row;
            return Map(created, [], new Dictionary<Guid, StaffAccessRecord>());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Team create failed; using in-memory.");
            return await _fallback.CreateAsync(name, slug, description, productSlug, ct);
        }
    }

    public async Task<TeamRecord?> AddMemberAsync(
        Guid teamId,
        Guid staffId,
        string memberRole,
        CancellationToken ct = default)
    {
        var role = NormalizeMemberRole(memberRole);
        try
        {
            var teamResponse = await _client.From<SbTeam>()
                .Filter("id", Operator.Equals, teamId.ToString())
                .Get();
            var team = (teamResponse.Models ?? []).FirstOrDefault();
            if (team is null)
                return await _fallback.AddMemberAsync(teamId, staffId, role, ct);

            var existing = await _client.From<SbTeamMember>()
                .Filter("team_id", Operator.Equals, teamId.ToString())
                .Filter("staff_id", Operator.Equals, staffId.ToString())
                .Get();

            if ((existing.Models ?? []).Count == 0)
            {
                await _client.From<SbTeamMember>().Insert(new SbTeamMember
                {
                    Id = Guid.NewGuid(),
                    TeamId = teamId,
                    StaffId = staffId,
                    MemberRole = role
                });
            }
            else
            {
                var row = existing.Models![0];
                row.MemberRole = role;
                await _client.From<SbTeamMember>()
                    .Filter("id", Operator.Equals, row.Id.ToString())
                    .Update(row);
            }

            var members = await _client.From<SbTeamMember>()
                .Filter("team_id", Operator.Equals, teamId.ToString())
                .Get();
            var staff = await _staff.ListAsync(ct);
            return Map(team, members.Models ?? [], staff.ToDictionary(s => s.Id));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Add team member failed; using in-memory.");
            return await _fallback.AddMemberAsync(teamId, staffId, role, ct);
        }
    }

    public async Task<bool> RemoveMemberAsync(Guid teamId, Guid staffId, CancellationToken ct = default)
    {
        try
        {
            var existing = await _client.From<SbTeamMember>()
                .Filter("team_id", Operator.Equals, teamId.ToString())
                .Filter("staff_id", Operator.Equals, staffId.ToString())
                .Get();

            var row = (existing.Models ?? []).FirstOrDefault();
            if (row is null)
                return await _fallback.RemoveMemberAsync(teamId, staffId, ct);

            await _client.From<SbTeamMember>()
                .Filter("id", Operator.Equals, row.Id.ToString())
                .Delete();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Remove team member failed; using in-memory.");
            return await _fallback.RemoveMemberAsync(teamId, staffId, ct);
        }
    }

    private static TeamRecord Map(
        SbTeam team,
        IEnumerable<SbTeamMember> members,
        IReadOnlyDictionary<Guid, StaffAccessRecord> staffById)
    {
        var mappedMembers = members
            .Where(m => m.TeamId == team.Id)
            .Select(m =>
            {
                staffById.TryGetValue(m.StaffId, out var staff);
                return new TeamMemberRecord
                {
                    StaffId = m.StaffId,
                    Email = staff?.Email ?? "",
                    DisplayName = staff?.DisplayName,
                    MemberRole = m.MemberRole
                };
            })
            .ToList();

        return new TeamRecord
        {
            Id = team.Id,
            Name = team.Name,
            Slug = team.Slug,
            Description = team.Description,
            ProductSlug = team.ProductSlug,
            Active = team.Active,
            Members = mappedMembers
        };
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
}
