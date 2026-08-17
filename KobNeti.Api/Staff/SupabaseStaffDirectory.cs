using Postgrest.Attributes;
using Postgrest.Models;
using static Postgrest.Constants;

namespace KobNeti.Api.Staff;

[Table("staff_profiles")]
public class SbStaffProfile : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("user_id")] public Guid? UserId { get; set; }
    [Column("email")] public string Email { get; set; } = string.Empty;
    [Column("display_name")] public string? DisplayName { get; set; }
    [Column("role")] public string Role { get; set; } = StaffRoles.Support;
    [Column("active")] public bool Active { get; set; } = true;
}

[Table("staff_product_access")]
public class SbStaffProductAccess : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("staff_id")] public Guid StaffId { get; set; }
    [Column("product_slug")] public string ProductSlug { get; set; } = string.Empty;
}

public class SupabaseStaffDirectory : IStaffDirectory
{
    private readonly Supabase.Client _client;
    private readonly InMemoryStaffDirectory _configFallback;
    private readonly ILogger<SupabaseStaffDirectory> _logger;

    public SupabaseStaffDirectory(
        Supabase.Client client,
        InMemoryStaffDirectory configFallback,
        ILogger<SupabaseStaffDirectory> logger)
    {
        _client = client;
        _configFallback = configFallback;
        _logger = logger;
    }

    public async Task<StaffAccessRecord?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim();
        try
        {
            var response = await _client.From<SbStaffProfile>()
                .Filter("email", Operator.Equals, normalized)
                .Filter("active", Operator.Equals, "true")
                .Get();

            var profile = (response.Models ?? []).FirstOrDefault();
            if (profile is null)
                return await _configFallback.FindByEmailAsync(normalized, ct);

            return await MapWithProductsAsync(profile, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Staff directory DB read failed for {Email}; using config.", normalized);
            return await _configFallback.FindByEmailAsync(normalized, ct);
        }
    }

    public async Task<IReadOnlyList<StaffAccessRecord>> ListAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _client.From<SbStaffProfile>()
                .Order("email", Ordering.Ascending)
                .Get();

            var profiles = response.Models ?? [];
            if (profiles.Count == 0)
                return await _configFallback.ListAsync(ct);

            var list = new List<StaffAccessRecord>();
            foreach (var profile in profiles)
                list.Add(await MapWithProductsAsync(profile, ct));
            return list;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Staff directory list failed; using config.");
            return await _configFallback.ListAsync(ct);
        }
    }

    public async Task<StaffAccessRecord> InviteAsync(
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
        var slugs = NormalizeSlugs(productSlugs);

        try
        {
            var existing = await _client.From<SbStaffProfile>()
                .Filter("email", Operator.Equals, normalizedEmail)
                .Get();

            var profile = (existing.Models ?? []).FirstOrDefault();
            if (profile is null)
            {
                profile = new SbStaffProfile
                {
                    Id = Guid.NewGuid(),
                    Email = normalizedEmail,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
                    Role = normalizedRole,
                    Active = true
                };
                var inserted = await _client.From<SbStaffProfile>().Insert(profile);
                profile = (inserted.Models ?? []).FirstOrDefault() ?? profile;
            }
            else
            {
                profile.DisplayName = string.IsNullOrWhiteSpace(displayName) ? profile.DisplayName : displayName.Trim();
                profile.Role = normalizedRole;
                profile.Active = true;
                await _client.From<SbStaffProfile>()
                    .Filter("id", Operator.Equals, profile.Id.ToString())
                    .Update(profile);
            }

            await ReplaceProductAccessAsync(profile.Id, slugs, ct);
            return await MapWithProductsAsync(profile, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Staff invite DB write failed; using in-memory fallback.");
            return await _configFallback.InviteAsync(normalizedEmail, displayName, normalizedRole, slugs, ct);
        }
    }

    public async Task<StaffAccessRecord?> SetActiveAsync(Guid staffId, bool active, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.From<SbStaffProfile>()
                .Filter("id", Operator.Equals, staffId.ToString())
                .Get();
            var profile = (response.Models ?? []).FirstOrDefault();
            if (profile is null)
                return await _configFallback.SetActiveAsync(staffId, active, ct);

            profile.Active = active;
            await _client.From<SbStaffProfile>()
                .Filter("id", Operator.Equals, staffId.ToString())
                .Update(profile);
            return await MapWithProductsAsync(profile, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Staff deactivate DB write failed for {StaffId}", staffId);
            return await _configFallback.SetActiveAsync(staffId, active, ct);
        }
    }

    public async Task<StaffAccessRecord?> SetProductAccessAsync(
        Guid staffId,
        IReadOnlyList<string> productSlugs,
        CancellationToken ct = default)
    {
        var slugs = NormalizeSlugs(productSlugs);
        try
        {
            var response = await _client.From<SbStaffProfile>()
                .Filter("id", Operator.Equals, staffId.ToString())
                .Get();
            var profile = (response.Models ?? []).FirstOrDefault();
            if (profile is null)
                return await _configFallback.SetProductAccessAsync(staffId, slugs, ct);

            await ReplaceProductAccessAsync(staffId, slugs, ct);
            return await MapWithProductsAsync(profile, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Staff product access DB write failed for {StaffId}", staffId);
            return await _configFallback.SetProductAccessAsync(staffId, slugs, ct);
        }
    }

    private async Task ReplaceProductAccessAsync(Guid staffId, IReadOnlyList<string> slugs, CancellationToken ct)
    {
        var existing = await _client.From<SbStaffProductAccess>()
            .Filter("staff_id", Operator.Equals, staffId.ToString())
            .Get();

        foreach (var row in existing.Models ?? [])
        {
            await _client.From<SbStaffProductAccess>()
                .Filter("id", Operator.Equals, row.Id.ToString())
                .Delete();
        }

        foreach (var slug in slugs)
        {
            await _client.From<SbStaffProductAccess>().Insert(new SbStaffProductAccess
            {
                Id = Guid.NewGuid(),
                StaffId = staffId,
                ProductSlug = slug
            });
        }
    }

    private async Task<StaffAccessRecord> MapWithProductsAsync(SbStaffProfile profile, CancellationToken ct)
    {
        var access = await _client.From<SbStaffProductAccess>()
            .Filter("staff_id", Operator.Equals, profile.Id.ToString())
            .Get();

        var slugs = (access.Models ?? [])
            .Select(a => a.ProductSlug)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new StaffAccessRecord
        {
            Id = profile.Id,
            UserId = profile.UserId,
            Email = profile.Email,
            DisplayName = profile.DisplayName,
            Role = profile.Role,
            Active = profile.Active,
            ProductSlugs = slugs
        };
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
}
