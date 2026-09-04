using KobNeti.Api.Products;

namespace KobNeti.Api.Data;

public class ChatSessionEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid? ExternalCustomerId { get; set; }
    public string? GuestName { get; set; }
    public string? GuestEmail { get; set; }
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public class ChatMessageEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid SessionId { get; set; }
    public string SenderType { get; set; } = string.Empty;
    public Guid? SenderId { get; set; }
    public string? SenderName { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ChatStickyNoteEntity
{
    public string TenantId { get; set; } = string.Empty;
    public Guid SessionId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string ReasonForContact { get; set; } = string.Empty;
    public string KeyActionsJson { get; set; } = "[]";
    public string ColorHex { get; set; } = "#F29D68";
    public bool Pinned { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

public class TicketEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string TicketNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Priority { get; set; } = "normal";
    public string Status { get; set; } = "open";
    public string? Team { get; set; }
    public Guid? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime? FirstResponseAt { get; set; }
    public Guid? ExternalCustomerId { get; set; }
    public string? PageUrl { get; set; }
    public string? AccountId { get; set; }
    public Guid? ChatSessionId { get; set; }
    /// <summary>Comma-separated or JSON list of tags (W2.6).</summary>
    public string? Tags { get; set; }
    public int? SlaFirstResponseMinutes { get; set; }
    public DateTime? FirstResponseDueAt { get; set; }
    public DateTime? ResolveDueAt { get; set; }
    public Guid? EngTaskId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TicketEventEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid TicketId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? ActorName { get; set; }
    public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TicketReplyEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid TicketId { get; set; }
    public string SenderType { get; set; } = "agent";
    public string? SenderName { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class KbArticleEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = "draft";
    public string? HeroImageUrl { get; set; }
    public int ViewCount { get; set; }
    public int HelpfulCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public class KbStepEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid ArticleId { get; set; }
    public int SortOrder { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}

public class KbCommentEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid ArticleId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class KbVoteEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid ArticleId { get; set; }
    public string VoterKey { get; set; } = string.Empty;
    public string Vote { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class MacroEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UploadEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class IncidentEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string IncidentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Severity { get; set; } = "sev3";
    public string Status { get; set; } = "open";
    public string? CommanderName { get; set; }
    public Guid? CommanderUserId { get; set; }
    public Guid? SourceTicketId { get; set; }
    public Guid? SourceChatSessionId { get; set; }
    public string? PostmortemNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class IncidentEventEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid IncidentId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? ActorName { get; set; }
    public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EngTaskEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string TaskNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TaskType { get; set; } = "feature";
    public string Status { get; set; } = "backlog";
    public string Priority { get; set; } = "medium";
    public decimal? EstimatePoints { get; set; }
    public string? AssigneeName { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public Guid? TicketId { get; set; }
    public Guid? MilestoneId { get; set; }
    public string? GithubPrUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class EngMilestoneEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "planned";
    public DateOnly? TargetDate { get; set; }
    public DateOnly? StartDate { get; set; }
    public int SortOrder { get; set; }
    public Guid? CalendarEventId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CalendarEventEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EventType { get; set; } = "milestone";
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public string? SourceEntityType { get; set; }
    public Guid? SourceEntityId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class GithubCacheEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string RepoKey { get; set; } = ProductRepoKinds.WebApp;
    public string RepoUrl { get; set; } = string.Empty;
    public string CacheKind { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "[]";
    public DateTime FetchedAt { get; set; }
}

public class TimeEntryEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string EntryType { get; set; } = "manual";
    public DateTime? ClockIn { get; set; }
    public DateTime? ClockOut { get; set; }
    public int? Minutes { get; set; }
    public Guid? TicketId { get; set; }
    public Guid? EngTaskId { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "approved";
    public Guid? SupersedesId { get; set; }
    public Guid? ApprovalId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ApprovalRequestEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string PayloadJson { get; set; } = "{}";
    public Guid? RequesterUserId { get; set; }
    public string? RequesterName { get; set; }
    public Guid? ApproverUserId { get; set; }
    public string? ApproverName { get; set; }
    public DateTime? DecidedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PayRateEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid? StaffId { get; set; }
    public Guid? UserId { get; set; }
    public string? Role { get; set; }
    public decimal HourlyRate { get; set; }
    public string Currency { get; set; } = "USD";
    public DateOnly EffectiveFrom { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PayPeriodEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public string Status { get; set; } = "open";
    public int TotalMinutes { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string LinesJson { get; set; } = "[]";
    public Guid? ApprovalId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AuditEventEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string Channel { get; set; } = "in_app";
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? LinkUrl { get; set; }
    public string? SourceType { get; set; }
    public string? SourceId { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationPrefsEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public bool AssignEnabled { get; set; } = true;
    public bool ApprovalEnabled { get; set; } = true;
    public bool EscalationEnabled { get; set; } = true;
    public bool ReminderEnabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
}

public class OpsFileEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string FolderPath { get; set; } = "/";
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string? PublicUrl { get; set; }
    public Guid? CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class IntegrationEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = "disconnected";
    public string ConfigJson { get; set; } = "{}";
    public DateTime? ConnectedAt { get; set; }
    public DateTime? DisconnectedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class IntegrationSecretEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PlatformHelpArticleEntity
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Category { get; set; } = "general";
    public string Status { get; set; } = "published";
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ReportRunEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string ParamsJson { get; set; } = "{}";
    public int RowCount { get; set; }
    public string CsvContent { get; set; } = string.Empty;
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ImChannelEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ChannelType { get; set; } = "channel";
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ImMessageEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid ChannelId { get; set; }
    public Guid? SenderUserId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AssetEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AssetType { get; set; } = "hardware";
    public string? SerialOrKey { get; set; }
    public string Status { get; set; } = "available";
    public Guid? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }
    public DateOnly? RenewalDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
