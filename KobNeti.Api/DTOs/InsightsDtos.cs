namespace KobNeti.Api.DTOs;

public class OverviewDTO
{
    public string TenantId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int ActiveChats { get; set; }
    public int OpenTickets { get; set; }
    public int OpenIncidents { get; set; }
    public int EngTasksInProgress { get; set; }
    public int PendingApprovals { get; set; }
    public int UnreadNotifications { get; set; }
}

public class CrossProductOverviewDTO
{
    public List<OverviewDTO> Products { get; set; } = [];
    public int TotalOpenTickets { get; set; }
    public int TotalActiveChats { get; set; }
    public int TotalOpenIncidents { get; set; }
    public int TotalPendingApprovals { get; set; }
}

public class ReportRunDTO
{
    public Guid Id { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedByName { get; set; }
}

public class RunReportDTO
{
    public string ReportType { get; set; } = "tickets";
    public string? Label { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public class PlatformHelpArticleDTO
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Category { get; set; } = "general";
    public string Status { get; set; } = "published";
    public int SortOrder { get; set; }
    public string? VideoUrl { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SavePlatformHelpDTO
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Category { get; set; } = "general";
    public string Status { get; set; } = "published";
    public int SortOrder { get; set; }
    public string? VideoUrl { get; set; }
}

public class PlatformHelpVideoDTO
{
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}

public class ImChannelDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ChannelType { get; set; } = "channel";
    public string Topic { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? LastMessageBody { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public string? LastSenderName { get; set; }
    public Guid? LastSenderUserId { get; set; }
}

public class CreateImChannelDTO
{
    public string Name { get; set; } = string.Empty;
    public string ChannelType { get; set; } = "channel";
    public string? Topic { get; set; }
}

public class OpenImDmDTO
{
    public Guid UserId { get; set; }
    public string? DisplayName { get; set; }
}

public class ImMessageDTO
{
    public Guid Id { get; set; }
    public Guid ChannelId { get; set; }
    public Guid? ParentMessageId { get; set; }
    public Guid? SenderUserId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SendImMessageDTO
{
    public string Body { get; set; } = string.Empty;
    public Guid? ParentMessageId { get; set; }
}

public class AssetDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AssetType { get; set; } = "hardware";
    public string? SerialOrKey { get; set; }
    public string Status { get; set; } = "available";
    public Guid? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }
    public DateOnly? RenewalDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SaveAssetDTO
{
    public string Name { get; set; } = string.Empty;
    public string AssetType { get; set; } = "hardware";
    public string? SerialOrKey { get; set; }
    public DateOnly? RenewalDate { get; set; }
    public string? Notes { get; set; }
}

public class AssignAssetDTO
{
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
}
