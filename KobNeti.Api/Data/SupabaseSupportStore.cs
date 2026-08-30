using KobNeti.Api.DTOs;
using static Postgrest.Constants;

namespace KobNeti.Api.Data;

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

    public async Task<ChatStickyNoteEntity?> GetChatStickyNoteAsync(string tenantId, Guid sessionId)
    {
        try
        {
            var response = await _client.From<SbChatStickyNote>()
                .Filter("tenant_id", Operator.Equals, tenantId)
                .Filter("session_id", Operator.Equals, sessionId.ToString())
                .Get();
            var model = response.Models.FirstOrDefault();
            return model is null ? null : ToEntity(model);
        }
        catch (Exception ex) when (IsMissingStickyNotesTable(ex))
        {
            return null;
        }
    }

    public async Task<ChatStickyNoteEntity> UpsertChatStickyNoteAsync(ChatStickyNoteEntity note)
    {
        try
        {
            var existing = await GetChatStickyNoteAsync(note.TenantId, note.SessionId);
            if (existing is null)
            {
                var inserted = await _client.From<SbChatStickyNote>().Insert(ToSb(note));
                var model = inserted.Models.FirstOrDefault();
                return model is null ? note : ToEntity(model);
            }

            await _client.From<SbChatStickyNote>()
                .Filter("tenant_id", Operator.Equals, note.TenantId)
                .Filter("session_id", Operator.Equals, note.SessionId.ToString())
                .Update(ToSb(note));
            return note;
        }
        catch (Exception ex) when (IsMissingStickyNotesTable(ex))
        {
            throw new InvalidOperationException(
                "Sticky notes storage is not set up. Apply supabase/support_w2_chat_sticky_notes.sql to your database.");
        }
    }

    public async Task DeleteChatStickyNoteAsync(string tenantId, Guid sessionId)
    {
        try
        {
            await _client.From<SbChatStickyNote>()
                .Filter("tenant_id", Operator.Equals, tenantId)
                .Filter("session_id", Operator.Equals, sessionId.ToString())
                .Delete();
        }
        catch (Exception ex) when (IsMissingStickyNotesTable(ex))
        {
            // Nothing to delete if storage was never provisioned.
        }
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

    public async Task<TicketEventEntity> InsertTicketEventAsync(TicketEventEntity evt)
    {
        var response = await _client.From<SbTicketEvent>().Insert(ToSb(evt));
        var model = response.Models.FirstOrDefault();
        return model is null ? evt : ToEntity(model);
    }

    public async Task<List<TicketEventEntity>> ListTicketEventsAsync(string tenantId, Guid ticketId)
    {
        var response = await _client.From<SbTicketEvent>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("ticket_id", Operator.Equals, ticketId.ToString())
            .Order("created_at", Ordering.Ascending)
            .Get();
        return (response.Models ?? []).Select(ToEntity).ToList();
    }

    public async Task<TicketStatsSnapshot> GetTicketStatsAsync(string tenantId)
    {
        var response = await _client.From<SbTicket>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Get();
        var items = response.Models ?? [];
        return new TicketStatsSnapshot(
            items.Count(t => TicketStatus.IsNewOrOpen(t.Status)),
            items.Count(t => t.Status == "in_progress"),
            items.Count,
            items.Count(t => t.Status == "waiting"));
    }

    public async Task<int> CountOpenTicketsAsync(string tenantId)
    {
        var response = await _client.From<SbTicket>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Get();
        return (response.Models ?? []).Count(t => TicketStatus.IsInbox(t.Status));
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

    public async Task<IncidentEntity> InsertIncidentAsync(IncidentEntity incident)
    {
        var response = await _client.From<SbIncident>().Insert(ToSb(incident));
        var model = response.Models.FirstOrDefault()
                    ?? throw new InvalidOperationException("Incident insert did not return a row.");
        return ToEntity(model);
    }

    public async Task<IncidentEntity?> GetIncidentAsync(string tenantId, Guid incidentId)
    {
        var response = await _client.From<SbIncident>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("id", Operator.Equals, incidentId.ToString())
            .Get();
        var model = (response.Models ?? []).FirstOrDefault();
        return model is null ? null : ToEntity(model);
    }

    public async Task UpdateIncidentAsync(IncidentEntity incident)
    {
        await _client.From<SbIncident>()
            .Filter("tenant_id", Operator.Equals, incident.TenantId)
            .Filter("id", Operator.Equals, incident.Id.ToString())
            .Update(ToSb(incident));
    }

    public async Task<(List<IncidentEntity> Items, int Total)> ListIncidentsAsync(
        string tenantId, string? status, int page, int pageSize)
    {
        var response = await _client.From<SbIncident>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Order("created_at", Ordering.Descending)
            .Get();
        var all = (response.Models ?? []).Select(ToEntity).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(status))
            all = all.Where(i => i.Status == status);
        var list = all.ToList();
        var total = list.Count;
        var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return (items, total);
    }

    public async Task<int> NextIncidentSequenceAsync(string tenantId)
    {
        var response = await _client.From<SbIncident>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Get();
        return (response.Models ?? []).Count + 1;
    }

    public async Task<IncidentEventEntity> InsertIncidentEventAsync(IncidentEventEntity evt)
    {
        var response = await _client.From<SbIncidentEvent>().Insert(ToSb(evt));
        var model = response.Models.FirstOrDefault();
        return model is null ? evt : ToEntity(model);
    }

    public async Task<List<IncidentEventEntity>> ListIncidentEventsAsync(string tenantId, Guid incidentId)
    {
        var response = await _client.From<SbIncidentEvent>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("incident_id", Operator.Equals, incidentId.ToString())
            .Order("created_at", Ordering.Ascending)
            .Get();
        return (response.Models ?? []).Select(ToEntity).ToList();
    }

    public async Task<EngTaskEntity> InsertEngTaskAsync(EngTaskEntity task)
    {
        var response = await _client.From<SbEngTask>().Insert(ToSb(task));
        var model = response.Models.FirstOrDefault();
        return model is null ? task : ToEntity(model);
    }

    public async Task<EngTaskEntity?> GetEngTaskAsync(string tenantId, Guid taskId)
    {
        var response = await _client.From<SbEngTask>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("id", Operator.Equals, taskId.ToString())
            .Get();
        var model = (response.Models ?? []).FirstOrDefault();
        return model is null ? null : ToEntity(model);
    }

    public async Task UpdateEngTaskAsync(EngTaskEntity task)
    {
        await _client.From<SbEngTask>()
            .Filter("tenant_id", Operator.Equals, task.TenantId)
            .Filter("id", Operator.Equals, task.Id.ToString())
            .Update(ToSb(task));
    }

    public async Task<(List<EngTaskEntity> Items, int Total)> ListEngTasksAsync(
        string tenantId, string? status, Guid? milestoneId, int page, int pageSize)
    {
        var response = await _client.From<SbEngTask>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Order("updated_at", Ordering.Descending)
            .Get();
        var all = (response.Models ?? []).Select(ToEntity).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(status))
            all = all.Where(t => t.Status == status);
        if (milestoneId.HasValue)
            all = all.Where(t => t.MilestoneId == milestoneId);
        var list = all.ToList();
        var total = list.Count;
        var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return (items, total);
    }

    public async Task<int> NextEngTaskSequenceAsync(string tenantId)
    {
        var response = await _client.From<SbEngTask>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Get();
        return (response.Models ?? []).Count + 1;
    }

    public async Task<EngMilestoneEntity> InsertMilestoneAsync(EngMilestoneEntity milestone)
    {
        var response = await _client.From<SbEngMilestone>().Insert(ToSb(milestone));
        var model = response.Models.FirstOrDefault();
        return model is null ? milestone : ToEntity(model);
    }

    public async Task<EngMilestoneEntity?> GetMilestoneAsync(string tenantId, Guid milestoneId)
    {
        var response = await _client.From<SbEngMilestone>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("id", Operator.Equals, milestoneId.ToString())
            .Get();
        var model = (response.Models ?? []).FirstOrDefault();
        return model is null ? null : ToEntity(model);
    }

    public async Task UpdateMilestoneAsync(EngMilestoneEntity milestone)
    {
        await _client.From<SbEngMilestone>()
            .Filter("tenant_id", Operator.Equals, milestone.TenantId)
            .Filter("id", Operator.Equals, milestone.Id.ToString())
            .Update(ToSb(milestone));
    }

    public async Task<List<EngMilestoneEntity>> ListMilestonesAsync(string tenantId)
    {
        var response = await _client.From<SbEngMilestone>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Order("sort_order", Ordering.Ascending)
            .Get();
        return (response.Models ?? []).Select(ToEntity)
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.TargetDate)
            .ToList();
    }

    public async Task<CalendarEventEntity> UpsertCalendarEventAsync(CalendarEventEntity evt)
    {
        var existing = await GetCalendarEventAsync(evt.TenantId, evt.Id);
        if (existing is null)
        {
            var response = await _client.From<SbCalendarEvent>().Insert(ToSb(evt));
            var model = response.Models.FirstOrDefault();
            return model is null ? evt : ToEntity(model);
        }

        await _client.From<SbCalendarEvent>()
            .Filter("tenant_id", Operator.Equals, evt.TenantId)
            .Filter("id", Operator.Equals, evt.Id.ToString())
            .Update(ToSb(evt));
        return evt;
    }

    public async Task<CalendarEventEntity?> GetCalendarEventAsync(string tenantId, Guid eventId)
    {
        var response = await _client.From<SbCalendarEvent>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("id", Operator.Equals, eventId.ToString())
            .Get();
        var model = (response.Models ?? []).FirstOrDefault();
        return model is null ? null : ToEntity(model);
    }

    public async Task DeleteCalendarEventAsync(string tenantId, Guid eventId)
    {
        await _client.From<SbCalendarEvent>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("id", Operator.Equals, eventId.ToString())
            .Delete();
    }

    public async Task<List<CalendarEventEntity>> ListCalendarEventsAsync(string tenantId)
    {
        var response = await _client.From<SbCalendarEvent>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Order("starts_at", Ordering.Ascending)
            .Get();
        return (response.Models ?? []).Select(ToEntity).ToList();
    }

    public async Task UpsertGithubCacheAsync(GithubCacheEntity cache)
    {
        var existing = await GetGithubCacheAsync(cache.TenantId, cache.CacheKind);
        if (existing is null)
        {
            await _client.From<SbGithubCache>().Insert(ToSb(cache));
            return;
        }

        cache.Id = existing.Id;
        await _client.From<SbGithubCache>()
            .Filter("tenant_id", Operator.Equals, cache.TenantId)
            .Filter("id", Operator.Equals, cache.Id.ToString())
            .Update(ToSb(cache));
    }

    public async Task<GithubCacheEntity?> GetGithubCacheAsync(string tenantId, string cacheKind)
    {
        var response = await _client.From<SbGithubCache>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("cache_kind", Operator.Equals, cacheKind)
            .Get();
        var model = (response.Models ?? []).FirstOrDefault();
        return model is null ? null : ToEntity(model);
    }

    public async Task<TimeEntryEntity> InsertTimeEntryAsync(TimeEntryEntity entry)
    {
        var response = await _client.From<SbTimeEntry>().Insert(ToSb(entry));
        var model = response.Models.FirstOrDefault();
        return model is null ? entry : ToEntity(model);
    }

    public async Task<TimeEntryEntity?> GetTimeEntryAsync(string tenantId, Guid id)
    {
        var response = await _client.From<SbTimeEntry>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("id", Operator.Equals, id.ToString())
            .Get();
        var model = (response.Models ?? []).FirstOrDefault();
        return model is null ? null : ToEntity(model);
    }

    public async Task UpdateTimeEntryAsync(TimeEntryEntity entry)
    {
        await _client.From<SbTimeEntry>()
            .Filter("tenant_id", Operator.Equals, entry.TenantId)
            .Filter("id", Operator.Equals, entry.Id.ToString())
            .Update(ToSb(entry));
    }

    public async Task<List<TimeEntryEntity>> ListTimeEntriesAsync(string tenantId, Guid? userId, string? status)
    {
        var response = await _client.From<SbTimeEntry>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Order("created_at", Ordering.Descending)
            .Get();
        var all = (response.Models ?? []).Select(ToEntity).AsEnumerable();
        if (userId.HasValue)
            all = all.Where(e => e.UserId == userId);
        if (!string.IsNullOrWhiteSpace(status))
            all = all.Where(e => e.Status == status);
        return all.ToList();
    }

    public async Task<TimeEntryEntity?> GetOpenClockAsync(string tenantId, Guid? userId)
    {
        var list = await ListTimeEntriesAsync(tenantId, userId, "open");
        return list.FirstOrDefault(e => e.EntryType == "clock");
    }

    public async Task<ApprovalRequestEntity> InsertApprovalAsync(ApprovalRequestEntity request)
    {
        var response = await _client.From<SbApprovalRequest>().Insert(ToSb(request));
        var model = response.Models.FirstOrDefault();
        return model is null ? request : ToEntity(model);
    }

    public async Task<ApprovalRequestEntity?> GetApprovalAsync(string tenantId, Guid id)
    {
        var response = await _client.From<SbApprovalRequest>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("id", Operator.Equals, id.ToString())
            .Get();
        var model = (response.Models ?? []).FirstOrDefault();
        return model is null ? null : ToEntity(model);
    }

    public async Task UpdateApprovalAsync(ApprovalRequestEntity request)
    {
        await _client.From<SbApprovalRequest>()
            .Filter("tenant_id", Operator.Equals, request.TenantId)
            .Filter("id", Operator.Equals, request.Id.ToString())
            .Update(ToSb(request));
    }

    public async Task<List<ApprovalRequestEntity>> ListApprovalsAsync(string tenantId, string? status)
    {
        var response = await _client.From<SbApprovalRequest>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Order("created_at", Ordering.Descending)
            .Get();
        var all = (response.Models ?? []).Select(ToEntity).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(status))
            all = all.Where(a => a.Status == status);
        return all.ToList();
    }

    public async Task<PayRateEntity> InsertPayRateAsync(PayRateEntity rate)
    {
        var response = await _client.From<SbPayRate>().Insert(ToSb(rate));
        var model = response.Models.FirstOrDefault();
        return model is null ? rate : ToEntity(model);
    }

    public async Task<List<PayRateEntity>> ListPayRatesAsync(string tenantId)
    {
        var response = await _client.From<SbPayRate>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Order("effective_from", Ordering.Descending)
            .Get();
        return (response.Models ?? []).Select(ToEntity).ToList();
    }

    public async Task<PayPeriodEntity> InsertPayPeriodAsync(PayPeriodEntity period)
    {
        var response = await _client.From<SbPayPeriod>().Insert(ToSb(period));
        var model = response.Models.FirstOrDefault();
        return model is null ? period : ToEntity(model);
    }

    public async Task<PayPeriodEntity?> GetPayPeriodAsync(string tenantId, Guid id)
    {
        var response = await _client.From<SbPayPeriod>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("id", Operator.Equals, id.ToString())
            .Get();
        var model = (response.Models ?? []).FirstOrDefault();
        return model is null ? null : ToEntity(model);
    }

    public async Task UpdatePayPeriodAsync(PayPeriodEntity period)
    {
        await _client.From<SbPayPeriod>()
            .Filter("tenant_id", Operator.Equals, period.TenantId)
            .Filter("id", Operator.Equals, period.Id.ToString())
            .Update(ToSb(period));
    }

    public async Task<List<PayPeriodEntity>> ListPayPeriodsAsync(string tenantId)
    {
        var response = await _client.From<SbPayPeriod>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Order("starts_on", Ordering.Descending)
            .Get();
        return (response.Models ?? []).Select(ToEntity).ToList();
    }

    public async Task InsertAuditEventAsync(AuditEventEntity evt) =>
        await _client.From<SbAuditEvent>().Insert(ToSb(evt));

    public async Task<List<AuditEventEntity>> ListAuditEventsAsync(
        string tenantId, string? action, string? entityType, string? search, int take)
    {
        var response = await _client.From<SbAuditEvent>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Order("created_at", Ordering.Descending)
            .Get();
        var all = (response.Models ?? []).Select(ToEntity).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(action))
            all = all.Where(e => e.Action == action);
        if (!string.IsNullOrWhiteSpace(entityType))
            all = all.Where(e => e.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            all = all.Where(e =>
                (e.EntityId?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                || (e.ActorName?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                || e.Action.Contains(s, StringComparison.OrdinalIgnoreCase)
                || (e.AfterJson?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        return all.Take(take).ToList();
    }

    public async Task InsertNotificationAsync(NotificationEntity n) =>
        await _client.From<SbNotification>().Insert(ToSb(n));

    public async Task<NotificationEntity?> GetNotificationAsync(string tenantId, Guid id)
    {
        var response = await _client.From<SbNotification>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("id", Operator.Equals, id.ToString())
            .Get();
        var model = (response.Models ?? []).FirstOrDefault();
        return model is null ? null : ToEntity(model);
    }

    public async Task UpdateNotificationAsync(NotificationEntity n)
    {
        await _client.From<SbNotification>()
            .Filter("tenant_id", Operator.Equals, n.TenantId)
            .Filter("id", Operator.Equals, n.Id.ToString())
            .Update(ToSb(n));
    }

    public async Task<List<NotificationEntity>> ListNotificationsAsync(string tenantId, Guid? userId, bool unreadOnly)
    {
        var response = await _client.From<SbNotification>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Order("created_at", Ordering.Descending)
            .Get();
        var all = (response.Models ?? []).Select(ToEntity).AsEnumerable();
        if (userId.HasValue)
            all = all.Where(n => n.UserId == null || n.UserId == userId);
        if (unreadOnly)
            all = all.Where(n => n.ReadAt is null);
        return all.ToList();
    }

    public async Task<NotificationPrefsEntity?> GetNotificationPrefsAsync(string tenantId, Guid userId)
    {
        var response = await _client.From<SbNotificationPrefs>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("user_id", Operator.Equals, userId.ToString())
            .Get();
        var model = (response.Models ?? []).FirstOrDefault();
        return model is null ? null : ToEntity(model);
    }

    public async Task UpsertNotificationPrefsAsync(NotificationPrefsEntity prefs)
    {
        var existing = await GetNotificationPrefsAsync(prefs.TenantId, prefs.UserId);
        if (existing is null)
        {
            await _client.From<SbNotificationPrefs>().Insert(ToSb(prefs));
            return;
        }
        prefs.Id = existing.Id;
        await _client.From<SbNotificationPrefs>()
            .Filter("id", Operator.Equals, prefs.Id.ToString())
            .Update(ToSb(prefs));
    }

    public async Task InsertOpsFileAsync(OpsFileEntity file) =>
        await _client.From<SbOpsFile>().Insert(ToSb(file));

    public async Task DeleteOpsFileAsync(string tenantId, Guid id)
    {
        await _client.From<SbOpsFile>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("id", Operator.Equals, id.ToString())
            .Delete();
    }

    public async Task<List<OpsFileEntity>> ListOpsFilesAsync(string tenantId, string? folderPath)
    {
        var response = await _client.From<SbOpsFile>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Order("created_at", Ordering.Descending)
            .Get();
        var all = (response.Models ?? []).Select(ToEntity).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(folderPath))
            all = all.Where(f => f.FolderPath == folderPath);
        return all.ToList();
    }

    public async Task<IntegrationEntity> UpsertIntegrationAsync(IntegrationEntity integration)
    {
        var existing = await GetIntegrationAsync(integration.TenantId, integration.Provider);
        if (existing is null)
        {
            var response = await _client.From<SbIntegration>().Insert(ToSb(integration));
            var model = response.Models.FirstOrDefault();
            return model is null ? integration : ToEntity(model);
        }
        integration.Id = existing.Id;
        await _client.From<SbIntegration>()
            .Filter("id", Operator.Equals, integration.Id.ToString())
            .Update(ToSb(integration));
        return integration;
    }

    public async Task<IntegrationEntity?> GetIntegrationAsync(string tenantId, string provider)
    {
        var response = await _client.From<SbIntegration>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("provider", Operator.Equals, provider)
            .Get();
        var model = (response.Models ?? []).FirstOrDefault();
        return model is null ? null : ToEntity(model);
    }

    public async Task<List<IntegrationEntity>> ListIntegrationsAsync(string tenantId)
    {
        var response = await _client.From<SbIntegration>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Get();
        return (response.Models ?? []).Select(ToEntity).ToList();
    }

    public async Task UpsertIntegrationSecretAsync(IntegrationSecretEntity secret)
    {
        var existing = (await ListIntegrationSecretsAsync(secret.TenantId, secret.Provider))
            .FirstOrDefault(s => s.SecretKey == secret.SecretKey);
        if (existing is null)
        {
            await _client.From<SbIntegrationSecret>().Insert(ToSb(secret));
            return;
        }
        secret.Id = existing.Id;
        await _client.From<SbIntegrationSecret>()
            .Filter("id", Operator.Equals, secret.Id.ToString())
            .Update(ToSb(secret));
    }

    public async Task DeleteIntegrationSecretsAsync(string tenantId, string provider)
    {
        await _client.From<SbIntegrationSecret>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("provider", Operator.Equals, provider)
            .Delete();
    }

    public async Task<List<IntegrationSecretEntity>> ListIntegrationSecretsAsync(string tenantId, string provider)
    {
        var response = await _client.From<SbIntegrationSecret>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("provider", Operator.Equals, provider)
            .Get();
        return (response.Models ?? []).Select(ToEntity).ToList();
    }

    public async Task<List<PlatformHelpArticleEntity>> ListPlatformHelpAsync(bool publishedOnly)
    {
        var response = await _client.From<SbPlatformHelp>().Get();
        var all = (response.Models ?? []).Select(ToEntity).AsEnumerable();
        if (publishedOnly)
            all = all.Where(a => a.Status == "published");
        return all.OrderBy(a => a.SortOrder).ThenBy(a => a.Title).ToList();
    }

    public async Task<PlatformHelpArticleEntity?> GetPlatformHelpBySlugAsync(string slug)
    {
        var response = await _client.From<SbPlatformHelp>()
            .Filter("slug", Operator.Equals, slug)
            .Get();
        var model = (response.Models ?? []).FirstOrDefault();
        return model is null ? null : ToEntity(model);
    }

    public async Task<PlatformHelpArticleEntity> UpsertPlatformHelpAsync(PlatformHelpArticleEntity article)
    {
        var existing = await GetPlatformHelpBySlugAsync(article.Slug);
        if (existing is null)
        {
            await _client.From<SbPlatformHelp>().Insert(ToSb(article));
            return article;
        }
        article.Id = existing.Id;
        await _client.From<SbPlatformHelp>()
            .Filter("id", Operator.Equals, article.Id.ToString())
            .Update(ToSb(article));
        return article;
    }

    public async Task InsertReportRunAsync(ReportRunEntity run) =>
        await _client.From<SbReportRun>().Insert(ToSb(run));

    public async Task<ReportRunEntity?> GetReportRunAsync(string tenantId, Guid id)
    {
        var response = await _client.From<SbReportRun>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("id", Operator.Equals, id.ToString())
            .Get();
        var model = (response.Models ?? []).FirstOrDefault();
        return model is null ? null : ToEntity(model);
    }

    public async Task<List<ReportRunEntity>> ListReportRunsAsync(string tenantId)
    {
        var response = await _client.From<SbReportRun>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Order("created_at", Ordering.Descending)
            .Get();
        return (response.Models ?? []).Select(ToEntity).ToList();
    }

    public async Task<ImChannelEntity> InsertImChannelAsync(ImChannelEntity channel)
    {
        await _client.From<SbImChannel>().Insert(ToSb(channel));
        return channel;
    }

    public async Task<ImChannelEntity?> GetImChannelAsync(string tenantId, Guid id)
    {
        var response = await _client.From<SbImChannel>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("id", Operator.Equals, id.ToString())
            .Get();
        var model = (response.Models ?? []).FirstOrDefault();
        return model is null ? null : ToEntity(model);
    }

    public async Task<List<ImChannelEntity>> ListImChannelsAsync(string tenantId)
    {
        var response = await _client.From<SbImChannel>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Order("name", Ordering.Ascending)
            .Get();
        return (response.Models ?? []).Select(ToEntity).ToList();
    }

    public async Task InsertImMessageAsync(ImMessageEntity message) =>
        await _client.From<SbImMessage>().Insert(ToSb(message));

    public async Task<List<ImMessageEntity>> ListImMessagesAsync(string tenantId, Guid channelId)
    {
        var response = await _client.From<SbImMessage>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("channel_id", Operator.Equals, channelId.ToString())
            .Order("created_at", Ordering.Ascending)
            .Get();
        return (response.Models ?? []).Select(ToEntity).ToList();
    }

    public async Task InsertAssetAsync(AssetEntity asset) =>
        await _client.From<SbAsset>().Insert(ToSb(asset));

    public async Task UpdateAssetAsync(AssetEntity asset)
    {
        await _client.From<SbAsset>()
            .Filter("tenant_id", Operator.Equals, asset.TenantId)
            .Filter("id", Operator.Equals, asset.Id.ToString())
            .Update(ToSb(asset));
    }

    public async Task<AssetEntity?> GetAssetAsync(string tenantId, Guid id)
    {
        var response = await _client.From<SbAsset>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Filter("id", Operator.Equals, id.ToString())
            .Get();
        var model = (response.Models ?? []).FirstOrDefault();
        return model is null ? null : ToEntity(model);
    }

    public async Task<List<AssetEntity>> ListAssetsAsync(string tenantId)
    {
        var response = await _client.From<SbAsset>()
            .Filter("tenant_id", Operator.Equals, tenantId)
            .Order("name", Ordering.Ascending)
            .Get();
        return (response.Models ?? []).Select(ToEntity).ToList();
    }

    public async Task<List<AssetEntity>> ListAssetsRenewingSoonAsync(string tenantId, DateOnly before)
    {
        var all = await ListAssetsAsync(tenantId);
        return all.Where(a =>
            a.RenewalDate.HasValue && a.RenewalDate <= before && a.Status != "retired").ToList();
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

    private static ChatStickyNoteEntity ToEntity(SbChatStickyNote m) => new()
    {
        TenantId = m.TenantId,
        SessionId = m.SessionId,
        AgentName = m.AgentName,
        ReasonForContact = m.ReasonForContact,
        KeyActionsJson = SerializeKeyActions(m.KeyActionsTaken),
        ColorHex = m.ColorHex,
        Pinned = m.Pinned,
        UpdatedAt = m.UpdatedAt,
        UpdatedBy = m.UpdatedBy
    };

    private static SbChatStickyNote ToSb(ChatStickyNoteEntity e) => new()
    {
        TenantId = e.TenantId,
        SessionId = e.SessionId,
        AgentName = e.AgentName,
        ReasonForContact = e.ReasonForContact,
        KeyActionsTaken = DeserializeKeyActions(e.KeyActionsJson),
        ColorHex = e.ColorHex,
        Pinned = e.Pinned,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy
    };

    private static string SerializeKeyActions(object? value)
    {
        if (value is null)
            return "[]";
        if (value is string s)
            return string.IsNullOrWhiteSpace(s) ? "[]" : s;
        return System.Text.Json.JsonSerializer.Serialize(value);
    }

    private static object DeserializeKeyActions(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return new List<string>();
        }
    }

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
        PageUrl = m.PageUrl,
        AccountId = m.AccountId,
        ChatSessionId = m.ChatSessionId,
        Tags = m.Tags,
        SlaFirstResponseMinutes = m.SlaFirstResponseMinutes,
        FirstResponseDueAt = m.FirstResponseDueAt,
        ResolveDueAt = m.ResolveDueAt,
        EngTaskId = m.EngTaskId,
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
        PageUrl = e.PageUrl,
        AccountId = e.AccountId,
        ChatSessionId = e.ChatSessionId,
        Tags = e.Tags,
        SlaFirstResponseMinutes = e.SlaFirstResponseMinutes,
        FirstResponseDueAt = e.FirstResponseDueAt,
        ResolveDueAt = e.ResolveDueAt,
        EngTaskId = e.EngTaskId,
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

    private static TicketEventEntity ToEntity(SbTicketEvent m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        TicketId = m.TicketId,
        EventType = m.EventType,
        ActorName = m.ActorName,
        Detail = m.Detail,
        CreatedAt = m.CreatedAt
    };

    private static SbTicketEvent ToSb(TicketEventEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        TicketId = e.TicketId,
        EventType = e.EventType,
        ActorName = e.ActorName,
        Detail = e.Detail,
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

    private static IncidentEntity ToEntity(SbIncident m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        IncidentNumber = m.IncidentNumber,
        Title = m.Title,
        Severity = m.Severity,
        Status = m.Status,
        CommanderName = m.CommanderName,
        CommanderUserId = m.CommanderUserId,
        SourceTicketId = m.SourceTicketId,
        SourceChatSessionId = m.SourceChatSessionId,
        PostmortemNotes = m.PostmortemNotes,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
        ResolvedAt = m.ResolvedAt
    };

    private static SbIncident ToSb(IncidentEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        IncidentNumber = e.IncidentNumber,
        Title = e.Title,
        Severity = e.Severity,
        Status = e.Status,
        CommanderName = e.CommanderName,
        CommanderUserId = e.CommanderUserId,
        SourceTicketId = e.SourceTicketId,
        SourceChatSessionId = e.SourceChatSessionId,
        PostmortemNotes = e.PostmortemNotes,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
        ResolvedAt = e.ResolvedAt
    };

    private static IncidentEventEntity ToEntity(SbIncidentEvent m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        IncidentId = m.IncidentId,
        EventType = m.EventType,
        ActorName = m.ActorName,
        Detail = m.Detail,
        CreatedAt = m.CreatedAt
    };

    private static SbIncidentEvent ToSb(IncidentEventEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        IncidentId = e.IncidentId,
        EventType = e.EventType,
        ActorName = e.ActorName,
        Detail = e.Detail,
        CreatedAt = e.CreatedAt
    };

    private static EngTaskEntity ToEntity(SbEngTask m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        TaskNumber = m.TaskNumber,
        Title = m.Title,
        Description = m.Description,
        TaskType = m.TaskType,
        Status = m.Status,
        Priority = m.Priority,
        EstimatePoints = m.EstimatePoints,
        AssigneeName = m.AssigneeName,
        AssigneeUserId = m.AssigneeUserId,
        TicketId = m.TicketId,
        MilestoneId = m.MilestoneId,
        GithubPrUrl = m.GithubPrUrl,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };

    private static SbEngTask ToSb(EngTaskEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        TaskNumber = e.TaskNumber,
        Title = e.Title,
        Description = e.Description,
        TaskType = e.TaskType,
        Status = e.Status,
        Priority = e.Priority,
        EstimatePoints = e.EstimatePoints,
        AssigneeName = e.AssigneeName,
        AssigneeUserId = e.AssigneeUserId,
        TicketId = e.TicketId,
        MilestoneId = e.MilestoneId,
        GithubPrUrl = e.GithubPrUrl,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };

    private static EngMilestoneEntity ToEntity(SbEngMilestone m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        Title = m.Title,
        Description = m.Description,
        Status = m.Status,
        TargetDate = m.TargetDate.HasValue ? DateOnly.FromDateTime(m.TargetDate.Value) : null,
        StartDate = m.StartDate.HasValue ? DateOnly.FromDateTime(m.StartDate.Value) : null,
        SortOrder = m.SortOrder,
        CalendarEventId = m.CalendarEventId,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };

    private static SbEngMilestone ToSb(EngMilestoneEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        Title = e.Title,
        Description = e.Description,
        Status = e.Status,
        TargetDate = e.TargetDate?.ToDateTime(TimeOnly.MinValue),
        StartDate = e.StartDate?.ToDateTime(TimeOnly.MinValue),
        SortOrder = e.SortOrder,
        CalendarEventId = e.CalendarEventId,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };

    private static CalendarEventEntity ToEntity(SbCalendarEvent m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        Title = m.Title,
        Description = m.Description,
        EventType = m.EventType,
        StartsAt = m.StartsAt,
        EndsAt = m.EndsAt,
        SourceEntityType = m.SourceEntityType,
        SourceEntityId = m.SourceEntityId,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };

    private static SbCalendarEvent ToSb(CalendarEventEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        Title = e.Title,
        Description = e.Description,
        EventType = e.EventType,
        StartsAt = e.StartsAt,
        EndsAt = e.EndsAt,
        SourceEntityType = e.SourceEntityType,
        SourceEntityId = e.SourceEntityId,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };

    private static GithubCacheEntity ToEntity(SbGithubCache m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        RepoUrl = m.RepoUrl,
        CacheKind = m.CacheKind,
        PayloadJson = m.PayloadJson,
        FetchedAt = m.FetchedAt
    };

    private static SbGithubCache ToSb(GithubCacheEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        RepoUrl = e.RepoUrl,
        CacheKind = e.CacheKind,
        PayloadJson = e.PayloadJson,
        FetchedAt = e.FetchedAt
    };

    private static TimeEntryEntity ToEntity(SbTimeEntry m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        UserId = m.UserId,
        UserName = m.UserName,
        EntryType = m.EntryType,
        ClockIn = m.ClockIn,
        ClockOut = m.ClockOut,
        Minutes = m.Minutes,
        TicketId = m.TicketId,
        EngTaskId = m.EngTaskId,
        Notes = m.Notes,
        Status = m.Status,
        SupersedesId = m.SupersedesId,
        ApprovalId = m.ApprovalId,
        CreatedAt = m.CreatedAt
    };

    private static SbTimeEntry ToSb(TimeEntryEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        UserId = e.UserId,
        UserName = e.UserName,
        EntryType = e.EntryType,
        ClockIn = e.ClockIn,
        ClockOut = e.ClockOut,
        Minutes = e.Minutes,
        TicketId = e.TicketId,
        EngTaskId = e.EngTaskId,
        Notes = e.Notes,
        Status = e.Status,
        SupersedesId = e.SupersedesId,
        ApprovalId = e.ApprovalId,
        CreatedAt = e.CreatedAt
    };

    private static ApprovalRequestEntity ToEntity(SbApprovalRequest m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        RequestType = m.RequestType,
        Status = m.Status,
        PayloadJson = m.PayloadJson,
        RequesterUserId = m.RequesterUserId,
        RequesterName = m.RequesterName,
        ApproverUserId = m.ApproverUserId,
        ApproverName = m.ApproverName,
        DecidedAt = m.DecidedAt,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };

    private static SbApprovalRequest ToSb(ApprovalRequestEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        RequestType = e.RequestType,
        Status = e.Status,
        PayloadJson = e.PayloadJson,
        RequesterUserId = e.RequesterUserId,
        RequesterName = e.RequesterName,
        ApproverUserId = e.ApproverUserId,
        ApproverName = e.ApproverName,
        DecidedAt = e.DecidedAt,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };

    private static PayRateEntity ToEntity(SbPayRate m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        StaffId = m.StaffId,
        UserId = m.UserId,
        Role = m.Role,
        HourlyRate = m.HourlyRate,
        Currency = m.Currency,
        EffectiveFrom = DateOnly.FromDateTime(m.EffectiveFrom),
        CreatedAt = m.CreatedAt
    };

    private static SbPayRate ToSb(PayRateEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        StaffId = e.StaffId,
        UserId = e.UserId,
        Role = e.Role,
        HourlyRate = e.HourlyRate,
        Currency = e.Currency,
        EffectiveFrom = e.EffectiveFrom.ToDateTime(TimeOnly.MinValue),
        CreatedAt = e.CreatedAt
    };

    private static PayPeriodEntity ToEntity(SbPayPeriod m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        Label = m.Label,
        StartsOn = DateOnly.FromDateTime(m.StartsOn),
        EndsOn = DateOnly.FromDateTime(m.EndsOn),
        Status = m.Status,
        TotalMinutes = m.TotalMinutes,
        TotalAmount = m.TotalAmount,
        Currency = m.Currency,
        LinesJson = m.LinesJson,
        ApprovalId = m.ApprovalId,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };

    private static SbPayPeriod ToSb(PayPeriodEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        Label = e.Label,
        StartsOn = e.StartsOn.ToDateTime(TimeOnly.MinValue),
        EndsOn = e.EndsOn.ToDateTime(TimeOnly.MinValue),
        Status = e.Status,
        TotalMinutes = e.TotalMinutes,
        TotalAmount = e.TotalAmount,
        Currency = e.Currency,
        LinesJson = e.LinesJson,
        ApprovalId = e.ApprovalId,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };

    private static AuditEventEntity ToEntity(SbAuditEvent m) => new()
    {
        Id = m.Id, TenantId = m.TenantId, ActorUserId = m.ActorUserId, ActorName = m.ActorName,
        Action = m.Action, EntityType = m.EntityType, EntityId = m.EntityId,
        BeforeJson = m.BeforeJson, AfterJson = m.AfterJson, CreatedAt = m.CreatedAt
    };
    private static SbAuditEvent ToSb(AuditEventEntity e) => new()
    {
        Id = e.Id, TenantId = e.TenantId, ActorUserId = e.ActorUserId, ActorName = e.ActorName,
        Action = e.Action, EntityType = e.EntityType, EntityId = e.EntityId,
        BeforeJson = e.BeforeJson, AfterJson = e.AfterJson, CreatedAt = e.CreatedAt
    };

    private static NotificationEntity ToEntity(SbNotification m) => new()
    {
        Id = m.Id, TenantId = m.TenantId, UserId = m.UserId, UserName = m.UserName, Channel = m.Channel,
        Title = m.Title, Body = m.Body, LinkUrl = m.LinkUrl, SourceType = m.SourceType, SourceId = m.SourceId,
        ReadAt = m.ReadAt, CreatedAt = m.CreatedAt
    };
    private static SbNotification ToSb(NotificationEntity e) => new()
    {
        Id = e.Id, TenantId = e.TenantId, UserId = e.UserId, UserName = e.UserName, Channel = e.Channel,
        Title = e.Title, Body = e.Body, LinkUrl = e.LinkUrl, SourceType = e.SourceType, SourceId = e.SourceId,
        ReadAt = e.ReadAt, CreatedAt = e.CreatedAt
    };

    private static NotificationPrefsEntity ToEntity(SbNotificationPrefs m) => new()
    {
        Id = m.Id, TenantId = m.TenantId, UserId = m.UserId,
        AssignEnabled = m.AssignEnabled, ApprovalEnabled = m.ApprovalEnabled,
        EscalationEnabled = m.EscalationEnabled, ReminderEnabled = m.ReminderEnabled, UpdatedAt = m.UpdatedAt
    };
    private static SbNotificationPrefs ToSb(NotificationPrefsEntity e) => new()
    {
        Id = e.Id, TenantId = e.TenantId, UserId = e.UserId,
        AssignEnabled = e.AssignEnabled, ApprovalEnabled = e.ApprovalEnabled,
        EscalationEnabled = e.EscalationEnabled, ReminderEnabled = e.ReminderEnabled, UpdatedAt = e.UpdatedAt
    };

    private static OpsFileEntity ToEntity(SbOpsFile m) => new()
    {
        Id = m.Id, TenantId = m.TenantId, FolderPath = m.FolderPath, FileName = m.FileName,
        ContentType = m.ContentType, SizeBytes = m.SizeBytes, StoragePath = m.StoragePath,
        PublicUrl = m.PublicUrl, CreatedBy = m.CreatedBy, CreatedByName = m.CreatedByName, CreatedAt = m.CreatedAt
    };
    private static SbOpsFile ToSb(OpsFileEntity e) => new()
    {
        Id = e.Id, TenantId = e.TenantId, FolderPath = e.FolderPath, FileName = e.FileName,
        ContentType = e.ContentType, SizeBytes = e.SizeBytes, StoragePath = e.StoragePath,
        PublicUrl = e.PublicUrl, CreatedBy = e.CreatedBy, CreatedByName = e.CreatedByName, CreatedAt = e.CreatedAt
    };

    private static IntegrationEntity ToEntity(SbIntegration m) => new()
    {
        Id = m.Id, TenantId = m.TenantId, Provider = m.Provider, DisplayName = m.DisplayName,
        Status = m.Status, ConfigJson = m.ConfigJson, ConnectedAt = m.ConnectedAt,
        DisconnectedAt = m.DisconnectedAt, UpdatedAt = m.UpdatedAt
    };
    private static SbIntegration ToSb(IntegrationEntity e) => new()
    {
        Id = e.Id, TenantId = e.TenantId, Provider = e.Provider, DisplayName = e.DisplayName,
        Status = e.Status, ConfigJson = e.ConfigJson, ConnectedAt = e.ConnectedAt,
        DisconnectedAt = e.DisconnectedAt, UpdatedAt = e.UpdatedAt
    };

    private static IntegrationSecretEntity ToEntity(SbIntegrationSecret m) => new()
    {
        Id = m.Id, TenantId = m.TenantId, Provider = m.Provider, SecretKey = m.SecretKey,
        Ciphertext = m.Ciphertext, CreatedAt = m.CreatedAt, UpdatedAt = m.UpdatedAt
    };
    private static SbIntegrationSecret ToSb(IntegrationSecretEntity e) => new()
    {
        Id = e.Id, TenantId = e.TenantId, Provider = e.Provider, SecretKey = e.SecretKey,
        Ciphertext = e.Ciphertext, CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt
    };

    private static PlatformHelpArticleEntity ToEntity(SbPlatformHelp m) => new()
    {
        Id = m.Id, Slug = m.Slug, Title = m.Title, Body = m.Body, Category = m.Category,
        Status = m.Status, SortOrder = m.SortOrder, CreatedAt = m.CreatedAt, UpdatedAt = m.UpdatedAt
    };
    private static SbPlatformHelp ToSb(PlatformHelpArticleEntity e) => new()
    {
        Id = e.Id, Slug = e.Slug, Title = e.Title, Body = e.Body, Category = e.Category,
        Status = e.Status, SortOrder = e.SortOrder, CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt
    };

    private static ReportRunEntity ToEntity(SbReportRun m) => new()
    {
        Id = m.Id, TenantId = m.TenantId, ReportType = m.ReportType, Label = m.Label,
        ParamsJson = m.ParamsJson, RowCount = m.RowCount, CsvContent = m.CsvContent,
        CreatedByName = m.CreatedByName, CreatedAt = m.CreatedAt
    };
    private static SbReportRun ToSb(ReportRunEntity e) => new()
    {
        Id = e.Id, TenantId = e.TenantId, ReportType = e.ReportType, Label = e.Label,
        ParamsJson = e.ParamsJson, RowCount = e.RowCount, CsvContent = e.CsvContent,
        CreatedByName = e.CreatedByName, CreatedAt = e.CreatedAt
    };

    private static ImChannelEntity ToEntity(SbImChannel m) => new()
    {
        Id = m.Id, TenantId = m.TenantId, Name = m.Name, ChannelType = m.ChannelType,
        CreatedBy = m.CreatedBy, CreatedAt = m.CreatedAt
    };
    private static SbImChannel ToSb(ImChannelEntity e) => new()
    {
        Id = e.Id, TenantId = e.TenantId, Name = e.Name, ChannelType = e.ChannelType,
        CreatedBy = e.CreatedBy, CreatedAt = e.CreatedAt
    };

    private static ImMessageEntity ToEntity(SbImMessage m) => new()
    {
        Id = m.Id, TenantId = m.TenantId, ChannelId = m.ChannelId, SenderUserId = m.SenderUserId,
        SenderName = m.SenderName, Body = m.Body, CreatedAt = m.CreatedAt
    };
    private static SbImMessage ToSb(ImMessageEntity e) => new()
    {
        Id = e.Id, TenantId = e.TenantId, ChannelId = e.ChannelId, SenderUserId = e.SenderUserId,
        SenderName = e.SenderName, Body = e.Body, CreatedAt = e.CreatedAt
    };

    private static AssetEntity ToEntity(SbAsset m) => new()
    {
        Id = m.Id, TenantId = m.TenantId, Name = m.Name, AssetType = m.AssetType,
        SerialOrKey = m.SerialOrKey, Status = m.Status, AssignedUserId = m.AssignedUserId,
        AssignedUserName = m.AssignedUserName,
        RenewalDate = m.RenewalDate.HasValue ? DateOnly.FromDateTime(m.RenewalDate.Value) : null,
        Notes = m.Notes,
        CreatedAt = m.CreatedAt, UpdatedAt = m.UpdatedAt
    };
    private static SbAsset ToSb(AssetEntity e) => new()
    {
        Id = e.Id, TenantId = e.TenantId, Name = e.Name, AssetType = e.AssetType,
        SerialOrKey = e.SerialOrKey, Status = e.Status, AssignedUserId = e.AssignedUserId,
        AssignedUserName = e.AssignedUserName,
        RenewalDate = e.RenewalDate?.ToDateTime(TimeOnly.MinValue),
        Notes = e.Notes,
        CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt
    };

    private static bool IsMissingStickyNotesTable(Exception ex)
    {
        var text = ex.ToString();
        return text.Contains("support_chat_sticky_notes", StringComparison.OrdinalIgnoreCase)
               || text.Contains("42P01", StringComparison.OrdinalIgnoreCase)
               || text.Contains("PGRST205", StringComparison.OrdinalIgnoreCase)
               || text.Contains("Could not find the table", StringComparison.OrdinalIgnoreCase);
    }
}
