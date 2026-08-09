using Postgrest.Attributes;
using Postgrest.Models;

namespace Sominnercore.SupportApi.Data;

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
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
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
