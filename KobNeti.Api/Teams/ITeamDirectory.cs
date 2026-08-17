namespace KobNeti.Api.Teams;

public class TeamRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ProductSlug { get; set; }
    public bool Active { get; set; } = true;
    public IReadOnlyList<TeamMemberRecord> Members { get; set; } = [];
}

public class TeamMemberRecord
{
    public Guid StaffId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string MemberRole { get; set; } = "member";
}

public interface ITeamDirectory
{
    Task<IReadOnlyList<TeamRecord>> ListAsync(CancellationToken ct = default);
    Task<TeamRecord> CreateAsync(string name, string? slug, string? description, string? productSlug, CancellationToken ct = default);
    Task<TeamRecord?> AddMemberAsync(Guid teamId, Guid staffId, string memberRole, CancellationToken ct = default);
    Task<bool> RemoveMemberAsync(Guid teamId, Guid staffId, CancellationToken ct = default);
}
