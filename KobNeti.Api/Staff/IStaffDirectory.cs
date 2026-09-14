using System.Text.Json;

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

    /// <summary>Roles allowed to sign in to the Operations platform.</summary>
    public static bool CanAccessOpsPlatform(string role) =>
        All.Contains(role)
        || string.Equals(role, Auth.AdminRoles.SupportTeam, StringComparison.OrdinalIgnoreCase);

    /// <summary>Roles allowed to use Support Hub / ops support APIs.</summary>
    public static bool CanUseSupportApis(string role) =>
        CanAccessOpsPlatform(role);

    public static List<string> NormalizeList(IEnumerable<string>? roles, string? primary = null, bool strict = true)
    {
        var list = new List<string>();
        void Add(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return;
            var match = All.FirstOrDefault(x => string.Equals(x, raw.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                if (strict)
                    throw new ArgumentException($"Invalid role '{raw}'. Allowed: {string.Join(", ", All)}");
                return;
            }
            if (!list.Contains(match, StringComparer.OrdinalIgnoreCase))
                list.Add(match);
        }

        foreach (var role in roles ?? [])
            Add(role);
        Add(primary);
        if (list.Count == 0)
            list.Add(Support);
        return list;
    }

    public static string Primary(IReadOnlyList<string> roles) =>
        roles.Count > 0 ? roles[0] : Support;

    public static List<string> FromJson(string? json, string? primary = null)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return NormalizeList(null, primary);
        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            return NormalizeList(parsed, primary, strict: false);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch
        {
            return NormalizeList(null, primary);
        }
    }

    public static string ToJson(IEnumerable<string> roles) =>
        JsonSerializer.Serialize(NormalizeList(roles));
}

public static class StaffStatuses
{
    public const string Active = "active";
    public const string Suspended = "suspended";
    public const string Deactivated = "deactivated";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Active, Suspended, Deactivated
    };

    public static string Normalize(string? status, bool? active = null)
    {
        if (!string.IsNullOrWhiteSpace(status) && All.Contains(status))
        {
            var normalized = All.First(x => string.Equals(x, status, StringComparison.OrdinalIgnoreCase));
            if (normalized == Active && active == false)
                return Deactivated;
            return normalized;
        }
        return active == false ? Deactivated : Active;
    }

    public static bool IsLoginAllowed(string status) =>
        string.Equals(status, Active, StringComparison.OrdinalIgnoreCase);

    public static bool ToActiveFlag(string status) => IsLoginAllowed(status);
}

public class StaffAccessRecord
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Role { get; set; } = StaffRoles.Support;
    public IReadOnlyList<string> Roles { get; set; } = [StaffRoles.Support];
    public string Status { get; set; } = StaffStatuses.Active;
    public bool Active { get; set; } = true;
    /// <summary>Empty means no products. Admin may still receive all via exchange logic.</summary>
    public IReadOnlyList<string> ProductSlugs { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastActiveAt { get; set; }
}

public class StaffInviteRecord
{
    public Guid Id { get; set; }
    public Guid? StaffId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Role { get; set; } = StaffRoles.Support;
    public IReadOnlyList<string> Roles { get; set; } = [StaffRoles.Support];
    public IReadOnlyList<string> ProductSlugs { get; set; } = [];
    public string? InvitedByEmail { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public bool IsPending =>
        AcceptedAt is null
        && CancelledAt is null
        && ExpiresAt > DateTime.UtcNow;
}

public interface IStaffDirectory
{
    Task<StaffAccessRecord?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<StaffAccessRecord>> ListAsync(CancellationToken ct = default);
    Task<StaffAccessRecord> InviteAsync(
        string email,
        string? displayName,
        string role,
        IReadOnlyList<string> productSlugs,
        CancellationToken ct = default,
        IReadOnlyList<string>? roles = null,
        string? invitedByEmail = null,
        bool recordInvite = true);
    Task<StaffAccessRecord?> SetActiveAsync(Guid staffId, bool active, CancellationToken ct = default);
    Task<StaffAccessRecord?> SetStatusAsync(Guid staffId, string status, CancellationToken ct = default);
    Task<StaffAccessRecord?> SetProductAccessAsync(Guid staffId, IReadOnlyList<string> productSlugs, CancellationToken ct = default);
    Task<StaffAccessRecord?> UpdateProfileAsync(
        Guid staffId,
        string? displayName,
        IReadOnlyList<string>? roles,
        IReadOnlyList<string>? productSlugs,
        string? status,
        CancellationToken ct = default);
    Task TouchLastActiveAsync(string email, CancellationToken ct = default);
    Task TouchLastActiveAsync(string email, Guid? authUserId, CancellationToken ct = default);
    Task<IReadOnlyList<StaffInviteRecord>> ListInvitesAsync(CancellationToken ct = default);
    Task<bool> CancelInviteAsync(Guid inviteId, CancellationToken ct = default);
    Task<StaffInviteRecord?> ResendInviteAsync(Guid inviteId, CancellationToken ct = default);
}
