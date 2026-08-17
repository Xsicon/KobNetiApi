namespace KobNeti.Api.Staff;

public static class StaffRoles
{
    public const string Admin = "admin";
    public const string Manager = "manager";
    public const string Engineer = "engineer";
    public const string Support = "support";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Admin, Manager, Engineer, Support
    };

    /// <summary>Roles allowed to use Support Hub / ops support APIs.</summary>
    public static bool CanUseSupportApis(string role) =>
        string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, Manager, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, Support, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, Auth.AdminRoles.SupportTeam, StringComparison.OrdinalIgnoreCase);
}

public class StaffAccessRecord
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Role { get; set; } = StaffRoles.Support;
    public bool Active { get; set; } = true;
    /// <summary>Empty means no products. Admin may still receive all via exchange logic.</summary>
    public IReadOnlyList<string> ProductSlugs { get; set; } = [];
}

public interface IStaffDirectory
{
    Task<StaffAccessRecord?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<StaffAccessRecord>> ListAsync(CancellationToken ct = default);
    Task<StaffAccessRecord> InviteAsync(string email, string? displayName, string role, IReadOnlyList<string> productSlugs, CancellationToken ct = default);
    Task<StaffAccessRecord?> SetActiveAsync(Guid staffId, bool active, CancellationToken ct = default);
    Task<StaffAccessRecord?> SetProductAccessAsync(Guid staffId, IReadOnlyList<string> productSlugs, CancellationToken ct = default);
}
