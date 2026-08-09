using static Postgrest.Constants;

namespace Sominnercore.SupportApi.Data;

public class SupabaseSupportStore : ISupportStore
{
    private readonly Supabase.Client _client;

    public SupabaseSupportStore(Supabase.Client client)
    {
        _client = client;
    }

    public async Task<ChatSessionEntity?> GetSessionAsync(string tenantId, Guid sessionId)
    {
        var response = await _client.From<SbChatSession>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("id", Operator.Equals, sessionId.ToString())
            .Get();
        var model = response.Models.FirstOrDefault();
        return model is null ? null : ToEntity(model);
    }

    public async Task<ChatSessionEntity> InsertSessionAsync(ChatSessionEntity session)
    {
        var response = await _client.From<SbChatSession>().Insert(ToSb(session));
        var model = response.Models.FirstOrDefault();
        return model is null ? session : ToEntity(model);
    }

    public async Task UpdateSessionAsync(ChatSessionEntity session)
    {
        await _client.From<SbChatSession>()
            .Filter("tenant_id", Operator.Equals, session.TenantId)
            .Filter("id", Operator.Equals, session.Id.ToString())
            .Update(ToSb(session));
    }

    public async Task<List<ChatSessionEntity>> ListActiveSessionsAsync(string tenantId)
    {
        var response = await _client.From<SbChatSession>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("status", Operator.Equals, "active")
            .Order("updated_at", Ordering.Descending)
            .Get();
        return response.Models.Select(ToEntity).ToList();
    }

    public async Task<ChatMessageEntity> InsertMessageAsync(ChatMessageEntity message)
    {
        var response = await _client.From<SbChatMessage>().Insert(ToSb(message));
        var model = response.Models.FirstOrDefault();
        return model is null ? message : ToEntity(model);
    }

    public async Task<List<ChatMessageEntity>> ListMessagesAsync(string tenantId, Guid sessionId)
    {
        var response = await _client.From<SbChatMessage>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("session_id", Operator.Equals, sessionId.ToString())
            .Order("created_at", Ordering.Ascending)
            .Get();
        return response.Models.Select(ToEntity).ToList();
    }

    public async Task<List<ChatMessageEntity>> ListMessagesForSessionsAsync(string tenantId, IEnumerable<Guid> sessionIds)
    {
        var ids = sessionIds.Select(id => (object)id.ToString()).ToList();
        if (ids.Count == 0)
            return [];

        var response = await _client.From<SbChatMessage>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("session_id", Operator.In, ids)
            .Get();
        return response.Models.Select(ToEntity).ToList();
    }

    public async Task<TicketEntity> InsertTicketAsync(TicketEntity ticket)
    {
        var response = await _client.From<SbTicket>().Insert(ToSb(ticket));
        var model = response.Models.FirstOrDefault();
        return model is null ? ticket : ToEntity(model);
    }

    public async Task<TicketEntity?> GetTicketAsync(string tenantId, Guid ticketId)
    {
        var response = await _client.From<SbTicket>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("id", Operator.Equals, ticketId.ToString())
            .Get();
        var model = response.Models.FirstOrDefault();
        return model is null ? null : ToEntity(model);
    }

    public async Task UpdateTicketAsync(TicketEntity ticket)
    {
        await _client.From<SbTicket>()
            .Filter("tenant_id", Operator.Equals, ticket.TenantId)
            .Filter("id", Operator.Equals, ticket.Id.ToString())
            .Update(ToSb(ticket));
    }

    public async Task<(List<TicketEntity> Items, int Total)> ListTicketsAsync(
        string tenantId, string? status, int page, int pageSize)
    {
        var query = _client.From<SbTicket>()
            .Filter("tenant_id", Operator.Equals, tenantId);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Filter("status", Operator.Equals, status);

        var response = await query.Order("created_at", Ordering.Descending).Get();
        var all = response.Models.Select(ToEntity).ToList();
        var total = all.Count;
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return (items, total);
    }

    public async Task<TicketReplyEntity> InsertReplyAsync(TicketReplyEntity reply)
    {
        var response = await _client.From<SbTicketReply>().Insert(ToSb(reply));
        var model = response.Models.FirstOrDefault();
        return model is null ? reply : ToEntity(model);
    }

    public async Task<List<TicketReplyEntity>> ListRepliesAsync(string tenantId, Guid ticketId)
    {
        var response = await _client.From<SbTicketReply>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("ticket_id", Operator.Equals, ticketId.ToString())
            .Order("created_at", Ordering.Ascending)
            .Get();
        return response.Models.Select(ToEntity).ToList();
    }

    public async Task<TicketStatsSnapshot> GetTicketStatsAsync(string tenantId)
    {
        var response = await _client.From<SbTicket>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Get();
        var items = response.Models;
        return new TicketStatsSnapshot(
            items.Count(t => t.Status == "open"),
            items.Count(t => t.Status == "in_progress"),
            items.Count);
    }

    public async Task<int> CountOpenTicketsAsync(string tenantId)
    {
        var response = await _client.From<SbTicket>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Get();
        return response.Models.Count(t => t.Status == "open" || t.Status == "in_progress");
    }

    public async Task<int> NextTicketSequenceAsync(string tenantId)
    {
        var response = await _client.From<SbTicket>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Get();
        return response.Models.Count + 1;
    }

    public async Task<KbArticleEntity> InsertArticleAsync(KbArticleEntity article)
    {
        var response = await _client.From<SbKbArticle>().Insert(ToSb(article));
        var model = response.Models.FirstOrDefault();
        return model is null ? article : ToEntity(model);
    }

    public async Task ReplaceStepsAsync(string tenantId, Guid articleId, List<KbStepEntity> steps)
    {
        var existing = await _client.From<SbKbStep>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("article_id", Operator.Equals, articleId.ToString())
            .Get();

        foreach (var step in existing.Models)
        {
            await _client.From<SbKbStep>()
                .Filter("tenant_id", Operator.Equals, tenantId)
                .Filter("id", Operator.Equals, step.Id.ToString())
                .Delete();
        }

        if (steps.Count == 0)
            return;

        await _client.From<SbKbStep>().Insert(steps.Select(ToSb).ToList());
    }

    public async Task<KbArticleEntity?> GetArticleAsync(string tenantId, Guid articleId)
    {
        var response = await _client.From<SbKbArticle>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("id", Operator.Equals, articleId.ToString())
            .Get();
        var model = response.Models.FirstOrDefault();
        return model is null ? null : ToEntity(model);
    }

    public async Task UpdateArticleAsync(KbArticleEntity article)
    {
        await _client.From<SbKbArticle>()
            .Filter("tenant_id", Operator.Equals, article.TenantId)
            .Filter("id", Operator.Equals, article.Id.ToString())
            .Update(ToSb(article));
    }

    public async Task DeleteArticleAsync(string tenantId, Guid articleId)
    {
        await _client.From<SbKbArticle>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("id", Operator.Equals, articleId.ToString())
            .Delete();
    }

    public async Task<(List<KbArticleEntity> Items, int Total)> ListArticlesAsync(
        string tenantId, string? category, string? status, string? search, int page, int pageSize, bool publishedOnly)
    {
        var response = await _client.From<SbKbArticle>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Get();

        IEnumerable<SbKbArticle> q = response.Models;
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

        var ordered = q.OrderByDescending(a => a.CreatedAt).Select(ToEntity).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return (items, total);
    }

    public async Task<List<KbStepEntity>> ListStepsAsync(string tenantId, Guid articleId)
    {
        var response = await _client.From<SbKbStep>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("article_id", Operator.Equals, articleId.ToString())
            .Order("sort_order", Ordering.Ascending)
            .Get();
        return response.Models.Select(ToEntity).ToList();
    }

    public async Task<KbCommentEntity> InsertCommentAsync(KbCommentEntity comment)
    {
        var response = await _client.From<SbKbComment>().Insert(ToSb(comment));
        var model = response.Models.FirstOrDefault();
        return model is null ? comment : ToEntity(model);
    }

    public async Task<List<KbCommentEntity>> ListCommentsAsync(string tenantId, Guid articleId)
    {
        var response = await _client.From<SbKbComment>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("article_id", Operator.Equals, articleId.ToString())
            .Order("created_at", Ordering.Ascending)
            .Get();
        return response.Models.Select(ToEntity).ToList();
    }

    public async Task UpsertVoteAsync(KbVoteEntity vote)
    {
        var response = await _client.From<SbKbVote>()
            .Filter("tenant_id", Operator.Equals, vote.TenantId)
            .Filter("article_id", Operator.Equals, vote.ArticleId.ToString())
            .Filter("voter_key", Operator.Equals, vote.VoterKey)
            .Get();

        var existing = response.Models.FirstOrDefault();
        if (existing is not null)
        {
            existing.Vote = vote.Vote;
            await _client.From<SbKbVote>()
                .Filter("tenant_id", Operator.Equals, vote.TenantId)
                .Filter("id", Operator.Equals, existing.Id.ToString())
                .Update(existing);
            return;
        }

        await _client.From<SbKbVote>().Insert(ToSb(vote));
    }

    public async Task<List<KbVoteEntity>> ListVotesAsync(string tenantId, Guid articleId)
    {
        var response = await _client.From<SbKbVote>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("article_id", Operator.Equals, articleId.ToString())
            .Get();
        return response.Models.Select(ToEntity).ToList();
    }

    public async Task<UploadEntity> InsertUploadAsync(UploadEntity upload)
    {
        var response = await _client.From<SbUpload>().Insert(ToSb(upload));
        var model = response.Models.FirstOrDefault();
        return model is null ? upload : ToEntity(model);
    }

    public async Task<List<MacroEntity>> ListMacrosAsync(string tenantId)
    {
        var response = await _client.From<SbMacro>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Order("updated_at", Ordering.Descending)
            .Get();
        return response.Models.Select(ToEntity).ToList();
    }

    public async Task<MacroEntity?> GetMacroAsync(string tenantId, Guid id)
    {
        var response = await _client.From<SbMacro>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("id", Operator.Equals, id.ToString())
            .Get();
        var model = response.Models.FirstOrDefault();
        return model is null ? null : ToEntity(model);
    }

    public async Task<MacroEntity> InsertMacroAsync(MacroEntity macro)
    {
        var response = await _client.From<SbMacro>().Insert(ToSb(macro));
        var model = response.Models.FirstOrDefault();
        return model is null ? macro : ToEntity(model);
    }

    public async Task UpdateMacroAsync(MacroEntity macro)
    {
        await _client.From<SbMacro>()
            .Filter("tenant_id", Operator.Equals, macro.TenantId)
            .Filter("id", Operator.Equals, macro.Id.ToString())
            .Update(ToSb(macro));
    }

    public async Task DeleteMacroAsync(string tenantId, Guid id)
    {
        await _client.From<SbMacro>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("id", Operator.Equals, id.ToString())
            .Delete();
    }

    public async Task<int> CountActiveChatsAsync(string tenantId)
    {
        var sessions = await ListActiveSessionsAsync(tenantId);
        if (sessions.Count == 0)
            return 0;

        var msgs = await ListMessagesForSessionsAsync(tenantId, sessions.Select(s => s.Id));
        var lastBySession = msgs
            .GroupBy(m => m.SessionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.CreatedAt).First());

        return sessions.Count(s =>
            !lastBySession.TryGetValue(s.Id, out var last) || last.SenderType == "customer");
    }

    private static ChatSessionEntity ToEntity(SbChatSession m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        ExternalCustomerId = m.ExternalCustomerId,
        GuestName = m.GuestName,
        GuestEmail = m.GuestEmail,
        Status = m.Status,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
        ClosedAt = m.ClosedAt
    };

    private static SbChatSession ToSb(ChatSessionEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        ExternalCustomerId = e.ExternalCustomerId,
        GuestName = e.GuestName,
        GuestEmail = e.GuestEmail,
        Status = e.Status,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
        ClosedAt = e.ClosedAt
    };

    private static ChatMessageEntity ToEntity(SbChatMessage m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        SessionId = m.SessionId,
        SenderType = m.SenderType,
        SenderId = m.SenderId,
        SenderName = m.SenderName,
        Message = m.Message,
        IsRead = m.IsRead,
        CreatedAt = m.CreatedAt
    };

    private static SbChatMessage ToSb(ChatMessageEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        SessionId = e.SessionId,
        SenderType = e.SenderType,
        SenderId = e.SenderId,
        SenderName = e.SenderName,
        Message = e.Message,
        IsRead = e.IsRead,
        CreatedAt = e.CreatedAt
    };

    private static TicketEntity ToEntity(SbTicket m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        TicketNumber = m.TicketNumber,
        Name = m.Name,
        Email = m.Email,
        Category = m.Category,
        Subject = m.Subject,
        Message = m.Message,
        Priority = m.Priority,
        Status = m.Status,
        Team = m.Team,
        AssignedTo = m.AssignedTo,
        AssignedToName = m.AssignedToName,
        FirstResponseAt = m.FirstResponseAt,
        ExternalCustomerId = m.ExternalCustomerId,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };

    private static SbTicket ToSb(TicketEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        TicketNumber = e.TicketNumber,
        Name = e.Name,
        Email = e.Email,
        Category = e.Category,
        Subject = e.Subject,
        Message = e.Message,
        Priority = e.Priority,
        Status = e.Status,
        Team = e.Team,
        AssignedTo = e.AssignedTo,
        AssignedToName = e.AssignedToName,
        FirstResponseAt = e.FirstResponseAt,
        ExternalCustomerId = e.ExternalCustomerId,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };

    private static TicketReplyEntity ToEntity(SbTicketReply m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        TicketId = m.TicketId,
        SenderType = m.SenderType,
        SenderName = m.SenderName,
        Message = m.Message,
        CreatedAt = m.CreatedAt
    };

    private static SbTicketReply ToSb(TicketReplyEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        TicketId = e.TicketId,
        SenderType = e.SenderType,
        SenderName = e.SenderName,
        Message = e.Message,
        CreatedAt = e.CreatedAt
    };

    private static KbArticleEntity ToEntity(SbKbArticle m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        Title = m.Title,
        Category = m.Category,
        Content = m.Content,
        Status = m.Status,
        HeroImageUrl = m.HeroImageUrl,
        ViewCount = m.ViewCount,
        HelpfulCount = m.HelpfulCount,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
        PublishedAt = m.PublishedAt
    };

    private static SbKbArticle ToSb(KbArticleEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        Title = e.Title,
        Category = e.Category,
        Content = e.Content,
        Status = e.Status,
        HeroImageUrl = e.HeroImageUrl,
        ViewCount = e.ViewCount,
        HelpfulCount = e.HelpfulCount,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
        PublishedAt = e.PublishedAt
    };

    private static KbStepEntity ToEntity(SbKbStep m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        ArticleId = m.ArticleId,
        SortOrder = m.SortOrder,
        Detail = m.Detail,
        ImageUrl = m.ImageUrl
    };

    private static SbKbStep ToSb(KbStepEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        ArticleId = e.ArticleId,
        SortOrder = e.SortOrder,
        Detail = e.Detail,
        ImageUrl = e.ImageUrl
    };

    private static KbCommentEntity ToEntity(SbKbComment m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        ArticleId = m.ArticleId,
        AuthorName = m.AuthorName,
        Body = m.Body,
        CreatedAt = m.CreatedAt
    };

    private static SbKbComment ToSb(KbCommentEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        ArticleId = e.ArticleId,
        AuthorName = e.AuthorName,
        Body = e.Body,
        CreatedAt = e.CreatedAt
    };

    private static KbVoteEntity ToEntity(SbKbVote m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        ArticleId = m.ArticleId,
        VoterKey = m.VoterKey,
        Vote = m.Vote,
        CreatedAt = m.CreatedAt
    };

    private static SbKbVote ToSb(KbVoteEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        ArticleId = e.ArticleId,
        VoterKey = e.VoterKey,
        Vote = e.Vote,
        CreatedAt = e.CreatedAt
    };

    private static MacroEntity ToEntity(SbMacro m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        Title = m.Title,
        Body = m.Body,
        Category = m.Category,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };

    private static SbMacro ToSb(MacroEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        Title = e.Title,
        Body = e.Body,
        Category = e.Category,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };

    private static UploadEntity ToEntity(SbUpload m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        Path = m.Path,
        PublicUrl = m.PublicUrl,
        ContentType = m.ContentType,
        SizeBytes = m.SizeBytes,
        CreatedBy = m.CreatedBy,
        CreatedAt = m.CreatedAt
    };

    private static SbUpload ToSb(UploadEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        Path = e.Path,
        PublicUrl = e.PublicUrl,
        ContentType = e.ContentType,
        SizeBytes = e.SizeBytes,
        CreatedBy = e.CreatedBy,
        CreatedAt = e.CreatedAt
    };
}
