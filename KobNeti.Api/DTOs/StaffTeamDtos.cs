namespace KobNeti.Api.DTOs;

public class StaffMemberDTO
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool Active { get; set; }
    public List<string> ProductSlugs { get; set; } = [];
}

public class InviteStaffDTO
{
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Role { get; set; } = "support";
    public List<string> ProductSlugs { get; set; } = [];
}

public class UpdateStaffProductsDTO
{
    public List<string> ProductSlugs { get; set; } = [];
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
