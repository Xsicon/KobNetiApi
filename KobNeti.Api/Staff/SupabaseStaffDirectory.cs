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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Staff directory DB read failed for {Email}; using config.", normalized);
            return await _configFallback.FindByEmailAsync(normalized, ct);
        }
    }
}
