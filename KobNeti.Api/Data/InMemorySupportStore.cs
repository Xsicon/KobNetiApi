using System.Collections.Concurrent;

namespace KobNeti.Api.Data;

public class InMemorySupportStore : ISupportStore
{
    private readonly ConcurrentDictionary<Guid, ChatSessionEntity> _sessions = new();
    private readonly ConcurrentDictionary<Guid, ChatMessageEntity> _messages = new();
    private readonly ConcurrentDictionary<Guid, TicketEntity> _tickets = new();
    private readonly ConcurrentDictionary<Guid, TicketReplyEntity> _replies = new();
    private readonly ConcurrentDictionary<Guid, KbArticleEntity> _articles = new();
    private readonly ConcurrentDictionary<Guid, KbStepEntity> _steps = new();
    private readonly ConcurrentDictionary<Guid, KbCommentEntity> _comments = new();
    private readonly ConcurrentDictionary<Guid, KbVoteEntity> _votes = new();
    private readonly ConcurrentDictionary<Guid, MacroEntity> _macros = new();
    private readonly ConcurrentDictionary<Guid, UploadEntity> _uploads = new();
    private readonly ConcurrentDictionary<string, int> _ticketSeq = new();

    public Task<ChatSessionEntity?> GetSessionAsync(string tenantId, Guid sessionId)
    {
        _sessions.TryGetValue(sessionId, out var s);
        return Task.FromResult(s is not null && s.TenantId == tenantId ? Clone(s) : null);
    }

    public Task<ChatSessionEntity> InsertSessionAsync(ChatSessionEntity session)
    {
        _sessions[session.Id] = Clone(session);
        return Task.FromResult(Clone(session));
    }

    public Task UpdateSessionAsync(ChatSessionEntity session)
    {
        if (_sessions.TryGetValue(session.Id, out var existing) && existing.TenantId == session.TenantId)
            _sessions[session.Id] = Clone(session);
        return Task.CompletedTask;
    }

    public Task<List<ChatSessionEntity>> ListActiveSessionsAsync(string tenantId) =>
        Task.FromResult(_sessions.Values
            .Where(s => s.TenantId == tenantId && s.Status == "active")
            .OrderByDescending(s => s.UpdatedAt)
            .Select(Clone)
            .ToList());

    public Task<ChatMessageEntity> InsertMessageAsync(ChatMessageEntity message)
    {
        _messages[message.Id] = Clone(message);
        return Task.FromResult(Clone(message));
    }

    public Task<List<ChatMessageEntity>> ListMessagesAsync(string tenantId, Guid sessionId) =>
        Task.FromResult(_messages.Values
            .Where(m => m.TenantId == tenantId && m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .Select(Clone)
            .ToList());

    public Task<List<ChatMessageEntity>> ListMessagesForSessionsAsync(string tenantId, IEnumerable<Guid> sessionIds)
    {
        var set = sessionIds.ToHashSet();
        return Task.FromResult(_messages.Values
            .Where(m => m.TenantId == tenantId && set.Contains(m.SessionId))
            .Select(Clone)
            .ToList());
    }

    public Task<TicketEntity> InsertTicketAsync(TicketEntity ticket)
    {
        _tickets[ticket.Id] = Clone(ticket);
        return Task.FromResult(Clone(ticket));
    }

    public Task<TicketEntity?> GetTicketAsync(string tenantId, Guid ticketId)
    {
        _tickets.TryGetValue(ticketId, out var t);
        return Task.FromResult(t is not null && t.TenantId == tenantId ? Clone(t) : null);
    }

    public Task UpdateTicketAsync(TicketEntity ticket)
    {
        if (_tickets.TryGetValue(ticket.Id, out var existing) && existing.TenantId == ticket.TenantId)
            _tickets[ticket.Id] = Clone(ticket);
        return Task.CompletedTask;
    }

    public Task<(List<TicketEntity> Items, int Total)> ListTicketsAsync(
        string tenantId, string? status, int page, int pageSize)
    {
        var q = _tickets.Values.Where(t => t.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(t => t.Status == status);
        var ordered = q.OrderByDescending(t => t.CreatedAt).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(Clone).ToList();
        return Task.FromResult((items, total));
    }

    public Task<TicketReplyEntity> InsertReplyAsync(TicketReplyEntity reply)
    {
        _replies[reply.Id] = Clone(reply);
        return Task.FromResult(Clone(reply));
    }

    public Task<List<TicketReplyEntity>> ListRepliesAsync(string tenantId, Guid ticketId) =>
        Task.FromResult(_replies.Values
            .Where(r => r.TenantId == tenantId && r.TicketId == ticketId)
            .OrderBy(r => r.CreatedAt)
            .Select(Clone)
            .ToList());

    public Task<TicketStatsSnapshot> GetTicketStatsAsync(string tenantId)
    {
        var items = _tickets.Values.Where(t => t.TenantId == tenantId).ToList();
        return Task.FromResult(new TicketStatsSnapshot(
            items.Count(t => t.Status == "open"),
            items.Count(t => t.Status == "in_progress"),
            items.Count));
    }

    public Task<int> CountOpenTicketsAsync(string tenantId) =>
        Task.FromResult(_tickets.Values.Count(t =>
            t.TenantId == tenantId && (t.Status == "open" || t.Status == "in_progress")));

    public Task<int> NextTicketSequenceAsync(string tenantId)
    {
        var next = _ticketSeq.AddOrUpdate(tenantId, 1, (_, n) => n + 1);
        return Task.FromResult(next);
    }

    public Task<KbArticleEntity> InsertArticleAsync(KbArticleEntity article)
    {
        _articles[article.Id] = Clone(article);
        return Task.FromResult(Clone(article));
    }

    public Task ReplaceStepsAsync(string tenantId, Guid articleId, List<KbStepEntity> steps)
    {
        foreach (var old in _steps.Values.Where(s => s.TenantId == tenantId && s.ArticleId == articleId).ToList())
            _steps.TryRemove(old.Id, out _);
        foreach (var step in steps)
            _steps[step.Id] = Clone(step);
        return Task.CompletedTask;
    }

    public Task<KbArticleEntity?> GetArticleAsync(string tenantId, Guid articleId)
    {
        _articles.TryGetValue(articleId, out var a);
        return Task.FromResult(a is not null && a.TenantId == tenantId ? Clone(a) : null);
    }

    public Task UpdateArticleAsync(KbArticleEntity article)
    {
        if (_articles.TryGetValue(article.Id, out var existing) && existing.TenantId == article.TenantId)
            _articles[article.Id] = Clone(article);
        return Task.CompletedTask;
    }

    public Task DeleteArticleAsync(string tenantId, Guid articleId)
    {
        if (_articles.TryGetValue(articleId, out var a) && a.TenantId == tenantId)
        {
            _articles.TryRemove(articleId, out _);
            foreach (var s in _steps.Values.Where(x => x.ArticleId == articleId).ToList())
                _steps.TryRemove(s.Id, out _);
            foreach (var c in _comments.Values.Where(x => x.ArticleId == articleId).ToList())
                _comments.TryRemove(c.Id, out _);
            foreach (var v in _votes.Values.Where(x => x.ArticleId == articleId).ToList())
                _votes.TryRemove(v.Id, out _);
        }
        return Task.CompletedTask;
    }

    public Task<(List<KbArticleEntity> Items, int Total)> ListArticlesAsync(
        string tenantId, string? category, string? status, string? search, int page, int pageSize, bool publishedOnly)
    {
        var q = _articles.Values.Where(a => a.TenantId == tenantId);
        if (publishedOnly)
            q = q.Where(a => a.Status == "published");
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(a => a.Status == status);
        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(a => string.Equals(a.Category, category, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(a =>
                a.Title.Contains(s, StringComparison.OrdinalIgnoreCase)
                || a.Content.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = q.OrderByDescending(a => a.CreatedAt).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(Clone).ToList();
        return Task.FromResult((items, total));
    }

    public Task<List<KbStepEntity>> ListStepsAsync(string tenantId, Guid articleId) =>
        Task.FromResult(_steps.Values
            .Where(s => s.TenantId == tenantId && s.ArticleId == articleId)
            .OrderBy(s => s.SortOrder)
            .Select(Clone)
            .ToList());

    public Task<KbCommentEntity> InsertCommentAsync(KbCommentEntity comment)
    {
        _comments[comment.Id] = Clone(comment);
        return Task.FromResult(Clone(comment));
    }

    public Task<List<KbCommentEntity>> ListCommentsAsync(string tenantId, Guid articleId) =>
        Task.FromResult(_comments.Values
            .Where(c => c.TenantId == tenantId && c.ArticleId == articleId)
            .OrderBy(c => c.CreatedAt)
            .Select(Clone)
            .ToList());

    public Task UpsertVoteAsync(KbVoteEntity vote)
    {
        var existing = _votes.Values.FirstOrDefault(v =>
            v.TenantId == vote.TenantId && v.ArticleId == vote.ArticleId && v.VoterKey == vote.VoterKey);
        if (existing is not null)
        {
            existing.Vote = vote.Vote;
            _votes[existing.Id] = Clone(existing);
        }
        else
        {
            _votes[vote.Id] = Clone(vote);
        }
        return Task.CompletedTask;
    }

    public Task<List<KbVoteEntity>> ListVotesAsync(string tenantId, Guid articleId) =>
        Task.FromResult(_votes.Values
            .Where(v => v.TenantId == tenantId && v.ArticleId == articleId)
            .Select(Clone)
            .ToList());

    public Task<UploadEntity> InsertUploadAsync(UploadEntity upload)
    {
        _uploads[upload.Id] = Clone(upload);
        return Task.FromResult(Clone(upload));
    }

    public Task<List<MacroEntity>> ListMacrosAsync(string tenantId) =>
        Task.FromResult(_macros.Values
            .Where(m => m.TenantId == tenantId)
            .OrderByDescending(m => m.UpdatedAt)
            .Select(Clone)
            .ToList());

    public Task<MacroEntity?> GetMacroAsync(string tenantId, Guid id)
    {
        _macros.TryGetValue(id, out var m);
        return Task.FromResult(m is not null && m.TenantId == tenantId ? Clone(m) : null);
    }

    public Task<MacroEntity> InsertMacroAsync(MacroEntity macro)
    {
        _macros[macro.Id] = Clone(macro);
        return Task.FromResult(Clone(macro));
    }

    public Task UpdateMacroAsync(MacroEntity macro)
    {
        if (_macros.TryGetValue(macro.Id, out var existing) && existing.TenantId == macro.TenantId)
            _macros[macro.Id] = Clone(macro);
        return Task.CompletedTask;
    }

    public Task DeleteMacroAsync(string tenantId, Guid id)
    {
        if (_macros.TryGetValue(id, out var m) && m.TenantId == tenantId)
            _macros.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public async Task<int> CountActiveChatsAsync(string tenantId)
    {
        var sessions = await ListActiveSessionsAsync(tenantId);
        var msgs = await ListMessagesForSessionsAsync(tenantId, sessions.Select(s => s.Id));
        var lastBySession = msgs
            .GroupBy(m => m.SessionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.CreatedAt).First());

        return sessions.Count(s =>
            !lastBySession.TryGetValue(s.Id, out var last) || last.SenderType == "customer");
    }

    private static T Clone<T>(T value) =>
        System.Text.Json.JsonSerializer.Deserialize<T>(
            System.Text.Json.JsonSerializer.Serialize(value))!;
}
