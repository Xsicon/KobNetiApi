namespace KobNeti.Api.Data;

/// <summary>
/// Tenant-scoped persistence. Every method requires tenantId and must filter by it.
/// </summary>
public interface ISupportStore
{
    // Chat
    Task<ChatSessionEntity?> GetSessionAsync(string tenantId, Guid sessionId);
    Task<ChatSessionEntity> InsertSessionAsync(ChatSessionEntity session);
    Task UpdateSessionAsync(ChatSessionEntity session);
    Task<List<ChatSessionEntity>> ListActiveSessionsAsync(string tenantId);
    Task<ChatMessageEntity> InsertMessageAsync(ChatMessageEntity message);
    Task<List<ChatMessageEntity>> ListMessagesAsync(string tenantId, Guid sessionId);
    Task<List<ChatMessageEntity>> ListMessagesForSessionsAsync(string tenantId, IEnumerable<Guid> sessionIds);

    // Tickets
    Task<TicketEntity> InsertTicketAsync(TicketEntity ticket);
    Task<TicketEntity?> GetTicketAsync(string tenantId, Guid ticketId);
    Task UpdateTicketAsync(TicketEntity ticket);
    Task<(List<TicketEntity> Items, int Total)> ListTicketsAsync(string tenantId, string? status, int page, int pageSize);
    Task<TicketReplyEntity> InsertReplyAsync(TicketReplyEntity reply);
    Task<List<TicketReplyEntity>> ListRepliesAsync(string tenantId, Guid ticketId);
    Task<TicketStatsSnapshot> GetTicketStatsAsync(string tenantId);
    Task<int> CountOpenTicketsAsync(string tenantId);
    Task<int> NextTicketSequenceAsync(string tenantId);

    // KB
    Task<KbArticleEntity> InsertArticleAsync(KbArticleEntity article);
    Task ReplaceStepsAsync(string tenantId, Guid articleId, List<KbStepEntity> steps);
    Task<KbArticleEntity?> GetArticleAsync(string tenantId, Guid articleId);
    Task UpdateArticleAsync(KbArticleEntity article);
    Task DeleteArticleAsync(string tenantId, Guid articleId);
    Task<(List<KbArticleEntity> Items, int Total)> ListArticlesAsync(
        string tenantId, string? category, string? status, string? search, int page, int pageSize, bool publishedOnly);
    Task<List<KbStepEntity>> ListStepsAsync(string tenantId, Guid articleId);
    Task<KbCommentEntity> InsertCommentAsync(KbCommentEntity comment);
    Task<List<KbCommentEntity>> ListCommentsAsync(string tenantId, Guid articleId);
    Task UpsertVoteAsync(KbVoteEntity vote);
    Task<List<KbVoteEntity>> ListVotesAsync(string tenantId, Guid articleId);
    Task<UploadEntity> InsertUploadAsync(UploadEntity upload);

    // Macros
    Task<List<MacroEntity>> ListMacrosAsync(string tenantId);
    Task<MacroEntity?> GetMacroAsync(string tenantId, Guid id);
    Task<MacroEntity> InsertMacroAsync(MacroEntity macro);
    Task UpdateMacroAsync(MacroEntity macro);
    Task DeleteMacroAsync(string tenantId, Guid id);

    Task<int> CountActiveChatsAsync(string tenantId);
}

public record TicketStatsSnapshot(int OpenCount, int InProgressCount, int TotalCount);
