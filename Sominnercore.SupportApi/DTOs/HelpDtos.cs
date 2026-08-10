namespace Sominnercore.SupportApi.DTOs;

public class SupportTicketDTO
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Team { get; set; }
    public Guid? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime? FirstResponseAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int ReplyCount { get; set; }
    public List<SupportTicketReplyDTO> Replies { get; set; } = [];
}

public class SupportTicketReplyDTO
{
    public Guid Id { get; set; }
    public string SenderType { get; set; } = "agent";
    public string? SenderName { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
}

public class UpdateTicketDTO
{
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? Team { get; set; }
    public Guid? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
}

public class AddTicketReplyDTO
{
    public string Message { get; set; } = string.Empty;
}

public class SubmitTicketDTO
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class UpdateTicketStatusDTO
{
    public string Status { get; set; } = string.Empty;
}

public class TicketStatsDTO
{
    public int OpenCount { get; set; }
    public int InProgressCount { get; set; }
    public int TotalCount { get; set; }
}

public static class TicketStatus
{
    public const string Open = "open";
    public const string InProgress = "in_progress";
    public const string Resolved = "resolved";
    public static readonly string[] All = [Open, InProgress, Resolved];
}

public static class TicketPriority
{
    public const string High = "high";
    public const string Normal = "normal";
    public const string Low = "low";
    public static readonly string[] All = [High, Normal, Low];

    public static string FromCategory(string category) =>
        category.ToLowerInvariant() switch
        {
            "orders" or "shipping" or "payments" or "returns" => High,
            _ => Normal
        };
}

public static class TicketSenderType
{
    public const string Customer = "customer";
    public const string Agent = "agent";
}

public class HelpArticleDTO
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = HelpArticleStatus.Draft;
    public string? HeroImageUrl { get; set; }
    public int ViewCount { get; set; }
    public int HelpfulCount { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public List<HelpArticleStepDTO> Steps { get; set; } = [];
    public List<HelpArticleCommentDTO> Comments { get; set; } = [];
    public int LikeCount { get; set; }
    public int DislikeCount { get; set; }
    public string? MyVote { get; set; }
}

public class HelpArticleStepDTO
{
    public Guid Id { get; set; }
    public int SortOrder { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}

public class HelpArticleCommentDTO
{
    public Guid Id { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AddHelpArticleCommentDTO
{
    public string Body { get; set; } = string.Empty;
}

public class HelpArticleVoteRequestDTO
{
    public string Vote { get; set; } = string.Empty;
}

public class HelpArticleEngagementDTO
{
    public int LikeCount { get; set; }
    public int DislikeCount { get; set; }
    public string? MyVote { get; set; }
    public List<HelpArticleCommentDTO> Comments { get; set; } = [];
}

public class SaveHelpArticleDTO
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = HelpArticleStatus.Draft;
    public string? HeroImageUrl { get; set; }
    public List<HelpArticleStepDTO> Steps { get; set; } = [];
}

public class UpdateHelpArticleStatusDTO
{
    public string Status { get; set; } = string.Empty;
}

public static class HelpArticleStatus
{
    public const string Draft = "draft";
    public const string Published = "published";
    public static readonly string[] All = [Draft, Published];

    public static string Normalize(string? status) =>
        string.Equals(status?.Trim(), Published, StringComparison.OrdinalIgnoreCase)
            ? Published
            : Draft;
}

public static class HelpArticleVoteValue
{
    public const string Like = "like";
    public const string Dislike = "dislike";
    public static readonly string[] All = [Like, Dislike];

    public static string? Normalize(string? vote)
    {
        if (string.IsNullOrWhiteSpace(vote)) return null;
        var trimmed = vote.Trim().ToLowerInvariant();
        return All.Contains(trimmed) ? trimmed : null;
    }
}

public class SupportCountsDTO
{
    public int ActiveChats { get; set; }
    public int OpenTickets { get; set; }
}

public class SupportTenantDTO
{
    public string TenantId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string PublicHelpCenterUrl { get; set; } = string.Empty;
}

public class SupportMacroDTO
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SaveMacroDTO
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Category { get; set; }
}

public class AgentTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class ExchangeTokenRequest
{
    public string AccessToken { get; set; } = string.Empty;
}
