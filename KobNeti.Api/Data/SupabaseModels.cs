using Postgrest.Attributes;
using Postgrest.Models;
using KobNeti.Api.Products;

namespace KobNeti.Api.Data;

[Table("support_chat_sessions")]
public class SbChatSession : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("external_customer_id")] public Guid? ExternalCustomerId { get; set; }
    [Column("guest_name")] public string? GuestName { get; set; }
    [Column("guest_email")] public string? GuestEmail { get; set; }
    [Column("status")] public string Status { get; set; } = "active";
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("closed_at")] public DateTime? ClosedAt { get; set; }
}

[Table("support_chat_messages")]
public class SbChatMessage : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("session_id")] public Guid SessionId { get; set; }
    [Column("sender_type")] public string SenderType { get; set; } = string.Empty;
    [Column("sender_id")] public Guid? SenderId { get; set; }
    [Column("sender_name")] public string? SenderName { get; set; }
    [Column("message")] public string Message { get; set; } = string.Empty;
    [Column("is_read")] public bool IsRead { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("support_chat_sticky_notes")]
public class SbChatStickyNote : BaseModel
{
    [PrimaryKey("session_id", false)] public Guid SessionId { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("agent_name")] public string AgentName { get; set; } = string.Empty;
    [Column("reason_for_contact")] public string ReasonForContact { get; set; } = string.Empty;
    [Column("key_actions_taken")] public object KeyActionsTaken { get; set; } = "[]";
    [Column("color_hex")] public string ColorHex { get; set; } = "#F29D68";
    [Column("pinned")] public bool Pinned { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("updated_by")] public Guid? UpdatedBy { get; set; }
}

[Table("support_tickets")]
public class SbTicket : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("ticket_number")] public string TicketNumber { get; set; } = string.Empty;
    [Column("name")] public string Name { get; set; } = string.Empty;
    [Column("email")] public string Email { get; set; } = string.Empty;
    [Column("category")] public string Category { get; set; } = string.Empty;
    [Column("subject")] public string Subject { get; set; } = string.Empty;
    [Column("message")] public string Message { get; set; } = string.Empty;
    [Column("priority")] public string Priority { get; set; } = "normal";
    [Column("status")] public string Status { get; set; } = "open";
    [Column("team")] public string? Team { get; set; }
    [Column("assigned_to")] public Guid? AssignedTo { get; set; }
    [Column("assigned_to_name")] public string? AssignedToName { get; set; }
    [Column("first_response_at")] public DateTime? FirstResponseAt { get; set; }
    [Column("external_customer_id")] public Guid? ExternalCustomerId { get; set; }
    [Column("page_url")] public string? PageUrl { get; set; }
    [Column("account_id")] public string? AccountId { get; set; }
    [Column("chat_session_id")] public Guid? ChatSessionId { get; set; }
    [Column("tags")] public string? Tags { get; set; }
    [Column("sla_first_response_minutes")] public int? SlaFirstResponseMinutes { get; set; }
    [Column("first_response_due_at")] public DateTime? FirstResponseDueAt { get; set; }
    [Column("resolve_due_at")] public DateTime? ResolveDueAt { get; set; }
    [Column("eng_task_id")] public Guid? EngTaskId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("support_ticket_events")]
public class SbTicketEvent : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("ticket_id")] public Guid TicketId { get; set; }
    [Column("event_type")] public string EventType { get; set; } = string.Empty;
    [Column("actor_name")] public string? ActorName { get; set; }
    [Column("detail")] public string? Detail { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("support_ticket_replies")]
public class SbTicketReply : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("ticket_id")] public Guid TicketId { get; set; }
    [Column("sender_type")] public string SenderType { get; set; } = "agent";
    [Column("sender_name")] public string? SenderName { get; set; }
    [Column("message")] public string Message { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("support_kb_articles")]
public class SbKbArticle : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("title")] public string Title { get; set; } = string.Empty;
    [Column("category")] public string Category { get; set; } = string.Empty;
    [Column("content")] public string Content { get; set; } = string.Empty;
    [Column("status")] public string Status { get; set; } = "draft";
    [Column("hero_image_url")] public string? HeroImageUrl { get; set; }
    [Column("view_count")] public int ViewCount { get; set; }
    [Column("helpful_count")] public int HelpfulCount { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("published_at")] public DateTime? PublishedAt { get; set; }
}

[Table("support_kb_article_steps")]
public class SbKbStep : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("article_id")] public Guid ArticleId { get; set; }
    [Column("sort_order")] public int SortOrder { get; set; }
    [Column("detail")] public string Detail { get; set; } = string.Empty;
    [Column("image_url")] public string? ImageUrl { get; set; }
}

[Table("support_kb_article_comments")]
public class SbKbComment : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("article_id")] public Guid ArticleId { get; set; }
    [Column("author_name")] public string AuthorName { get; set; } = string.Empty;
    [Column("body")] public string Body { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("support_kb_article_votes")]
public class SbKbVote : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("article_id")] public Guid ArticleId { get; set; }
    [Column("voter_key")] public string VoterKey { get; set; } = string.Empty;
    [Column("vote")] public string Vote { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("support_macros")]
public class SbMacro : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("title")] public string Title { get; set; } = string.Empty;
    [Column("body")] public string Body { get; set; } = string.Empty;
    [Column("category")] public string? Category { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("support_uploads")]
public class SbUpload : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("path")] public string Path { get; set; } = string.Empty;
    [Column("public_url")] public string PublicUrl { get; set; } = string.Empty;
    [Column("content_type")] public string? ContentType { get; set; }
    [Column("size_bytes")] public long? SizeBytes { get; set; }
    [Column("created_by")] public Guid? CreatedBy { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("support_incidents")]
public class SbIncident : BaseModel
{
    [PrimaryKey("id")] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("incident_number")] public string IncidentNumber { get; set; } = string.Empty;
    [Column("title")] public string Title { get; set; } = string.Empty;
    [Column("severity")] public string Severity { get; set; } = "sev3";
    [Column("status")] public string Status { get; set; } = "open";
    [Column("commander_name")] public string? CommanderName { get; set; }
    [Column("commander_user_id")] public Guid? CommanderUserId { get; set; }
    [Column("source_ticket_id")] public Guid? SourceTicketId { get; set; }
    [Column("source_chat_session_id")] public Guid? SourceChatSessionId { get; set; }
    [Column("postmortem_notes")] public string? PostmortemNotes { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("resolved_at")] public DateTime? ResolvedAt { get; set; }
}

[Table("support_incident_events")]
public class SbIncidentEvent : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("incident_id")] public Guid IncidentId { get; set; }
    [Column("event_type")] public string EventType { get; set; } = string.Empty;
    [Column("actor_name")] public string? ActorName { get; set; }
    [Column("detail")] public string? Detail { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("eng_tasks")]
public class SbEngTask : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("task_number")] public string TaskNumber { get; set; } = string.Empty;
    [Column("title")] public string Title { get; set; } = string.Empty;
    [Column("description")] public string? Description { get; set; }
    [Column("task_type")] public string TaskType { get; set; } = "feature";
    [Column("status")] public string Status { get; set; } = "backlog";
    [Column("priority")] public string Priority { get; set; } = "medium";
    [Column("estimate_points")] public decimal? EstimatePoints { get; set; }
    [Column("assignee_name")] public string? AssigneeName { get; set; }
    [Column("assignee_user_id")] public Guid? AssigneeUserId { get; set; }
    [Column("ticket_id")] public Guid? TicketId { get; set; }
    [Column("milestone_id")] public Guid? MilestoneId { get; set; }
    [Column("github_pr_url")] public string? GithubPrUrl { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("eng_milestones")]
public class SbEngMilestone : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("title")] public string Title { get; set; } = string.Empty;
    [Column("description")] public string? Description { get; set; }
    [Column("status")] public string Status { get; set; } = "planned";
    [Column("target_date")] public DateTime? TargetDate { get; set; }
    [Column("start_date")] public DateTime? StartDate { get; set; }
    [Column("sort_order")] public int SortOrder { get; set; }
    [Column("calendar_event_id")] public Guid? CalendarEventId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("ops_calendar_events")]
public class SbCalendarEvent : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("title")] public string Title { get; set; } = string.Empty;
    [Column("description")] public string? Description { get; set; }
    [Column("event_type")] public string EventType { get; set; } = "milestone";
    [Column("starts_at")] public DateTime StartsAt { get; set; }
    [Column("ends_at")] public DateTime? EndsAt { get; set; }
    [Column("location")] public string? Location { get; set; }
    [Column("source_entity_type")] public string? SourceEntityType { get; set; }
    [Column("source_entity_id")] public Guid? SourceEntityId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("eng_github_cache")]
public class SbGithubCache : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("repo_key")] public string RepoKey { get; set; } = ProductRepoKinds.WebApp;
    [Column("repo_url")] public string RepoUrl { get; set; } = string.Empty;
    [Column("cache_kind")] public string CacheKind { get; set; } = string.Empty;
    [Column("payload_json")] public string PayloadJson { get; set; } = "[]";
    [Column("fetched_at")] public DateTime FetchedAt { get; set; }
}

/// <summary>Pre–product_repos migration shape (no repo_key column).</summary>
[Table("eng_github_cache")]
public class SbGithubCacheLegacy : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("repo_url")] public string RepoUrl { get; set; } = string.Empty;
    [Column("cache_kind")] public string CacheKind { get; set; } = string.Empty;
    [Column("payload_json")] public string PayloadJson { get; set; } = "[]";
    [Column("fetched_at")] public DateTime FetchedAt { get; set; }
}

[Table("ops_time_entries")]
public class SbTimeEntry : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("user_id")] public Guid? UserId { get; set; }
    [Column("user_name")] public string UserName { get; set; } = string.Empty;
    [Column("entry_type")] public string EntryType { get; set; } = "manual";
    [Column("clock_in")] public DateTime? ClockIn { get; set; }
    [Column("clock_out")] public DateTime? ClockOut { get; set; }
    [Column("minutes")] public int? Minutes { get; set; }
    [Column("ticket_id")] public Guid? TicketId { get; set; }
    [Column("eng_task_id")] public Guid? EngTaskId { get; set; }
    [Column("notes")] public string? Notes { get; set; }
    [Column("status")] public string Status { get; set; } = "approved";
    [Column("supersedes_id")] public Guid? SupersedesId { get; set; }
    [Column("approval_id")] public Guid? ApprovalId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("ops_approval_requests")]
public class SbApprovalRequest : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("request_type")] public string RequestType { get; set; } = string.Empty;
    [Column("status")] public string Status { get; set; } = "pending";
    [Column("payload_json")] public string PayloadJson { get; set; } = "{}";
    [Column("requester_user_id")] public Guid? RequesterUserId { get; set; }
    [Column("requester_name")] public string? RequesterName { get; set; }
    [Column("approver_user_id")] public Guid? ApproverUserId { get; set; }
    [Column("approver_name")] public string? ApproverName { get; set; }
    [Column("decided_at")] public DateTime? DecidedAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("ops_pay_rates")]
public class SbPayRate : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("staff_id")] public Guid? StaffId { get; set; }
    [Column("user_id")] public Guid? UserId { get; set; }
    [Column("role")] public string? Role { get; set; }
    [Column("hourly_rate")] public decimal HourlyRate { get; set; }
    [Column("currency")] public string Currency { get; set; } = "USD";
    [Column("effective_from")] public DateTime EffectiveFrom { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("ops_pay_periods")]
public class SbPayPeriod : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("label")] public string Label { get; set; } = string.Empty;
    [Column("starts_on")] public DateTime StartsOn { get; set; }
    [Column("ends_on")] public DateTime EndsOn { get; set; }
    [Column("status")] public string Status { get; set; } = "open";
    [Column("total_minutes")] public int TotalMinutes { get; set; }
    [Column("total_amount")] public decimal TotalAmount { get; set; }
    [Column("currency")] public string Currency { get; set; } = "USD";
    [Column("lines_json")] public string LinesJson { get; set; } = "[]";
    [Column("approval_id")] public Guid? ApprovalId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("ops_audit_events")]
public class SbAuditEvent : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("actor_user_id")] public Guid? ActorUserId { get; set; }
    [Column("actor_name")] public string? ActorName { get; set; }
    [Column("action")] public string Action { get; set; } = string.Empty;
    [Column("entity_type")] public string EntityType { get; set; } = string.Empty;
    [Column("entity_id")] public string? EntityId { get; set; }
    [Column("before_json")] public string? BeforeJson { get; set; }
    [Column("after_json")] public string? AfterJson { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("ops_notifications")]
public class SbNotification : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("user_id")] public Guid? UserId { get; set; }
    [Column("user_name")] public string? UserName { get; set; }
    [Column("channel")] public string Channel { get; set; } = "in_app";
    [Column("title")] public string Title { get; set; } = string.Empty;
    [Column("body")] public string? Body { get; set; }
    [Column("link_url")] public string? LinkUrl { get; set; }
    [Column("source_type")] public string? SourceType { get; set; }
    [Column("source_id")] public string? SourceId { get; set; }
    [Column("read_at")] public DateTime? ReadAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("ops_notification_preferences")]
public class SbNotificationPrefs : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("user_id")] public Guid UserId { get; set; }
    [Column("assign_enabled")] public bool AssignEnabled { get; set; } = true;
    [Column("approval_enabled")] public bool ApprovalEnabled { get; set; } = true;
    [Column("escalation_enabled")] public bool EscalationEnabled { get; set; } = true;
    [Column("reminder_enabled")] public bool ReminderEnabled { get; set; } = true;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("ops_files")]
public class SbOpsFile : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("folder_path")] public string FolderPath { get; set; } = "/";
    [Column("file_name")] public string FileName { get; set; } = string.Empty;
    [Column("content_type")] public string? ContentType { get; set; }
    [Column("size_bytes")] public long? SizeBytes { get; set; }
    [Column("storage_path")] public string StoragePath { get; set; } = string.Empty;
    [Column("public_url")] public string? PublicUrl { get; set; }
    [Column("created_by")] public Guid? CreatedBy { get; set; }
    [Column("created_by_name")] public string? CreatedByName { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("access")] public string Access { get; set; } = "restricted";
}

[Table("ops_integrations")]
public class SbIntegration : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("provider")] public string Provider { get; set; } = string.Empty;
    [Column("display_name")] public string DisplayName { get; set; } = string.Empty;
    [Column("status")] public string Status { get; set; } = "disconnected";
    [Column("config_json")] public string ConfigJson { get; set; } = "{}";
    [Column("connected_at")] public DateTime? ConnectedAt { get; set; }
    [Column("disconnected_at")] public DateTime? DisconnectedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("ops_integration_secrets")]
public class SbIntegrationSecret : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("provider")] public string Provider { get; set; } = string.Empty;
    [Column("secret_key")] public string SecretKey { get; set; } = string.Empty;
    [Column("ciphertext")] public string Ciphertext { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("ops_help_articles")]
public class SbPlatformHelp : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("slug")] public string Slug { get; set; } = string.Empty;
    [Column("title")] public string Title { get; set; } = string.Empty;
    [Column("body")] public string Body { get; set; } = string.Empty;
    [Column("category")] public string Category { get; set; } = "general";
    [Column("status")] public string Status { get; set; } = "published";
    [Column("sort_order")] public int SortOrder { get; set; }
    [Column("video_url")] public string? VideoUrl { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("ops_report_runs")]
public class SbReportRun : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("report_type")] public string ReportType { get; set; } = string.Empty;
    [Column("label")] public string Label { get; set; } = string.Empty;
    [Column("params_json")] public string ParamsJson { get; set; } = "{}";
    [Column("row_count")] public int RowCount { get; set; }
    [Column("csv_content")] public string CsvContent { get; set; } = string.Empty;
    [Column("created_by_name")] public string? CreatedByName { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("ops_im_channels")]
public class SbImChannel : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("name")] public string Name { get; set; } = string.Empty;
    [Column("channel_type")] public string ChannelType { get; set; } = "channel";
    [Column("topic")] public string Topic { get; set; } = string.Empty;
    [Column("created_by")] public Guid? CreatedBy { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("ops_im_messages")]
public class SbImMessage : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("channel_id")] public Guid ChannelId { get; set; }
    [Column("parent_message_id")] public Guid? ParentMessageId { get; set; }
    [Column("sender_user_id")] public Guid? SenderUserId { get; set; }
    [Column("sender_name")] public string SenderName { get; set; } = string.Empty;
    [Column("body")] public string Body { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("ops_assets")]
public class SbAsset : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("tenant_id")] public string TenantId { get; set; } = string.Empty;
    [Column("name")] public string Name { get; set; } = string.Empty;
    [Column("asset_type")] public string AssetType { get; set; } = "hardware";
    [Column("serial_or_key")] public string? SerialOrKey { get; set; }
    [Column("status")] public string Status { get; set; } = "available";
    [Column("assigned_user_id")] public Guid? AssignedUserId { get; set; }
    [Column("assigned_user_name")] public string? AssignedUserName { get; set; }
    [Column("renewal_date")] public DateTime? RenewalDate { get; set; }
    [Column("notes")] public string? Notes { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}
