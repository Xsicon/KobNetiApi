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
    private readonly List<StaffAccessRecord> _staff;

    public InMemoryStaffDirectory(IOptions<SupportOptions> options)
    {
        _staff = (options.Value.Staff ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s.Email))
            .Select(s => new StaffAccessRecord
            {
                Id = Guid.NewGuid(),
                Email = s.Email.Trim(),
                DisplayName = s.DisplayName,
                Role = string.IsNullOrWhiteSpace(s.Role) ? StaffRoles.Support : s.Role.Trim(),
                Active = true,
                ProductSlugs = (s.ProductSlugs ?? [])
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .ToList();
    }

    public Task<StaffAccessRecord?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var hit = _staff.FirstOrDefault(s =>
            s.Active && string.Equals(s.Email, email.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(hit);
    }
}
