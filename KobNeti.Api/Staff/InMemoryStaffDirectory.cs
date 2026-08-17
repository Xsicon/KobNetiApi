using Microsoft.Extensions.Options;
using KobNeti.Api.Tenancy;

namespace KobNeti.Api.Staff;

public class StaffAssignmentConfig
{
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = StaffRoles.Support;
    public string? DisplayName { get; set; }
    public string[] ProductSlugs { get; set; } = [];
}

public class InMemoryStaffDirectory : IStaffDirectory
{
    private readonly List<StaffAccessRecord> _staff = [];
    private readonly object _gate = new();

    public InMemoryStaffDirectory(IOptions<SupportOptions> options)
    {
        foreach (var s in options.Value.Staff ?? [])
        {
            if (string.IsNullOrWhiteSpace(s.Email))
                continue;

            _staff.Add(new StaffAccessRecord
            {
                Id = Guid.NewGuid(),
                Email = s.Email.Trim(),
                DisplayName = s.DisplayName,
                Role = string.IsNullOrWhiteSpace(s.Role) ? StaffRoles.Support : s.Role.Trim(),
                Active = true,
                ProductSlugs = NormalizeSlugs(s.ProductSlugs)
            });
        }
    }

    public Task<StaffAccessRecord?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var hit = _staff.FirstOrDefault(s =>
                s.Active && string.Equals(s.Email, email.Trim(), StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(Clone(hit));
        }
    }

    public Task<IReadOnlyList<StaffAccessRecord>> ListAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            IReadOnlyList<StaffAccessRecord> list = _staff
                .OrderBy(s => s.Email, StringComparer.OrdinalIgnoreCase)
                .Select(Clone)
                .Where(s => s is not null)
                .Cast<StaffAccessRecord>()
                .ToList();
            return Task.FromResult(list);
        }
    }

    public Task<StaffAccessRecord> InviteAsync(
        string email,
        string? displayName,
        string role,
        IReadOnlyList<string> productSlugs,
        CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
            throw new ArgumentException("Email is required.");

        var normalizedRole = NormalizeRole(role);

        lock (_gate)
        {
            var existing = _staff.FirstOrDefault(s =>
                string.Equals(s.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.DisplayName = string.IsNullOrWhiteSpace(displayName) ? existing.DisplayName : displayName.Trim();
                existing.Role = normalizedRole;
                existing.Active = true;
                existing.ProductSlugs = NormalizeSlugs(productSlugs);
                return Task.FromResult(Clone(existing)!);
            }

            var created = new StaffAccessRecord
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
                Role = normalizedRole,
                Active = true,
                ProductSlugs = NormalizeSlugs(productSlugs)
            };
            _staff.Add(created);
            return Task.FromResult(Clone(created)!);
        }
    }

    public Task<StaffAccessRecord?> SetActiveAsync(Guid staffId, bool active, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var hit = _staff.FirstOrDefault(s => s.Id == staffId);
            if (hit is null)
                return Task.FromResult<StaffAccessRecord?>(null);
            hit.Active = active;
            return Task.FromResult(Clone(hit));
        }
    }

    public Task<StaffAccessRecord?> SetProductAccessAsync(
        Guid staffId,
        IReadOnlyList<string> productSlugs,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            var hit = _staff.FirstOrDefault(s => s.Id == staffId);
            if (hit is null)
                return Task.FromResult<StaffAccessRecord?>(null);
            hit.ProductSlugs = NormalizeSlugs(productSlugs);
            return Task.FromResult(Clone(hit));
        }
    }

    private static string NormalizeRole(string role)
    {
        var r = string.IsNullOrWhiteSpace(role) ? StaffRoles.Support : role.Trim();
        if (!StaffRoles.All.Contains(r))
            throw new ArgumentException($"Invalid role '{r}'. Allowed: {string.Join(", ", StaffRoles.All)}");
        return StaffRoles.All.First(x => string.Equals(x, r, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> NormalizeSlugs(IEnumerable<string>? slugs) =>
        (slugs ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static StaffAccessRecord? Clone(StaffAccessRecord? s) =>
        s is null
            ? null
            : new StaffAccessRecord
            {
                Id = s.Id,
                UserId = s.UserId,
                Email = s.Email,
                DisplayName = s.DisplayName,
                Role = s.Role,
                Active = s.Active,
                ProductSlugs = s.ProductSlugs.ToList()
            };
}
