namespace KobNeti.Api.DTOs;

public class StaffMemberDTO
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Role { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
    public string Status { get; set; } = "active";
    public bool Active { get; set; }
    public List<string> ProductSlugs { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public bool EmailSent { get; set; }
}

public class StaffInviteDTO
{
    public Guid Id { get; set; }
    public Guid? StaffId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Role { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
    public List<string> ProductSlugs { get; set; } = [];
    public string? InvitedByEmail { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    /// <summary>pending | expiring_soon | accepted | revoked | expired</summary>
    public string Status { get; set; } = "pending";
    public bool ExpiringSoon { get; set; }
    public bool WaitingOver24h { get; set; }
    public bool EmailSent { get; set; }
}

public class StaffStatsDTO
{
    public int TotalUsers { get; set; }
    public int AddedThisMonth { get; set; }
    public int ActiveCount { get; set; }
    public int SuspendedCount { get; set; }
    public int DeactivatedCount { get; set; }
    public int PendingInvites { get; set; }
    public int ExpiringSoon { get; set; }
    public int WaitingOver24h { get; set; }
    public int AdminCount { get; set; }
    public int ManagerCount { get; set; }
    public int EngineerCount { get; set; }
    public int SupportCount { get; set; }
}

public class InviteStaffDTO
{
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Role { get; set; } = "support";
    public List<string> Roles { get; set; } = [];
    public List<string> ProductSlugs { get; set; } = [];
    public Guid? TeamId { get; set; }
    public string? InviteRedirectUrl { get; set; }
}

public class ResendStaffInviteDTO
{
    public string? InviteRedirectUrl { get; set; }
}

public class UpdateStaffProductsDTO
{
    public List<string> ProductSlugs { get; set; } = [];
}

public class UpdateStaffStatusDTO
{
    public string Status { get; set; } = "active";
}

public class UpdateStaffProfileDTO
{
    public string? DisplayName { get; set; }
    public List<string>? Roles { get; set; }
    public List<string>? ProductSlugs { get; set; }
    public string? Status { get; set; }
}

public class TeamDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ProductSlug { get; set; }
    public bool Active { get; set; }
    public List<TeamMemberDTO> Members { get; set; } = [];
}

public class TeamMemberDTO
{
    public Guid StaffId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string MemberRole { get; set; } = "member";
}

public class CreateTeamDTO
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string? ProductSlug { get; set; }
}

public class AddTeamMemberDTO
{
    public Guid StaffId { get; set; }
    public string MemberRole { get; set; } = "member";
}

public class RotateEmbedKeyDTO
{
    public string PublicKey { get; set; } = string.Empty;
    public string WidgetSnippet { get; set; } = string.Empty;
}
