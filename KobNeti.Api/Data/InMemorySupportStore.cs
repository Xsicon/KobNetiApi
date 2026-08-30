using System.Collections.Concurrent;

namespace KobNeti.Api.Data;

public class InMemorySupportStore : ISupportStore
{
    private readonly ConcurrentDictionary<Guid, ChatSessionEntity> _sessions = new();
    private readonly ConcurrentDictionary<Guid, ChatMessageEntity> _messages = new();
    private readonly ConcurrentDictionary<Guid, TicketEntity> _tickets = new();
    private readonly ConcurrentDictionary<Guid, TicketReplyEntity> _replies = new();
    private readonly ConcurrentDictionary<Guid, TicketEventEntity> _ticketEvents = new();
    private readonly ConcurrentDictionary<Guid, KbArticleEntity> _articles = new();
    private readonly ConcurrentDictionary<Guid, KbStepEntity> _steps = new();
    private readonly ConcurrentDictionary<Guid, KbCommentEntity> _comments = new();
    private readonly ConcurrentDictionary<Guid, KbVoteEntity> _votes = new();
    private readonly ConcurrentDictionary<Guid, MacroEntity> _macros = new();
    private readonly ConcurrentDictionary<Guid, UploadEntity> _uploads = new();
    private readonly ConcurrentDictionary<Guid, IncidentEntity> _incidents = new();
    private readonly ConcurrentDictionary<Guid, IncidentEventEntity> _incidentEvents = new();
    private readonly ConcurrentDictionary<Guid, EngTaskEntity> _engTasks = new();
    private readonly ConcurrentDictionary<Guid, EngMilestoneEntity> _milestones = new();
    private readonly ConcurrentDictionary<Guid, CalendarEventEntity> _calendarEvents = new();
    private readonly ConcurrentDictionary<string, GithubCacheEntity> _githubCache = new();
    private readonly ConcurrentDictionary<Guid, TimeEntryEntity> _timeEntries = new();
    private readonly ConcurrentDictionary<Guid, ApprovalRequestEntity> _approvals = new();
    private readonly ConcurrentDictionary<Guid, PayRateEntity> _payRates = new();
    private readonly ConcurrentDictionary<Guid, PayPeriodEntity> _payPeriods = new();
    private readonly ConcurrentDictionary<Guid, AuditEventEntity> _audit = new();
    private readonly ConcurrentDictionary<Guid, NotificationEntity> _notifications = new();
    private readonly ConcurrentDictionary<string, NotificationPrefsEntity> _notifPrefs = new();
    private readonly ConcurrentDictionary<Guid, OpsFileEntity> _opsFiles = new();
    private readonly ConcurrentDictionary<string, IntegrationEntity> _integrations = new();
    private readonly ConcurrentDictionary<string, IntegrationSecretEntity> _integrationSecrets = new();
    private readonly ConcurrentDictionary<Guid, PlatformHelpArticleEntity> _platformHelp = new();
    private readonly ConcurrentDictionary<Guid, ReportRunEntity> _reportRuns = new();
    private readonly ConcurrentDictionary<Guid, ImChannelEntity> _imChannels = new();
    private readonly ConcurrentDictionary<Guid, ImMessageEntity> _imMessages = new();
    private readonly ConcurrentDictionary<Guid, AssetEntity> _assets = new();
    private readonly ConcurrentDictionary<string, int> _ticketSeq = new();
    private readonly ConcurrentDictionary<string, int> _incidentSeq = new();
    private readonly ConcurrentDictionary<string, int> _engTaskSeq = new();
    private readonly ConcurrentDictionary<string, ChatStickyNoteEntity> _chatStickyNotes = new();

    private static string StickyKey(string tenantId, Guid sessionId) => $"{tenantId}|{sessionId}";

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

    public Task<ChatStickyNoteEntity?> GetChatStickyNoteAsync(string tenantId, Guid sessionId)
    {
        _chatStickyNotes.TryGetValue(StickyKey(tenantId, sessionId), out var note);
        return Task.FromResult(note is not null ? Clone(note) : null);
    }

    public Task<ChatStickyNoteEntity> UpsertChatStickyNoteAsync(ChatStickyNoteEntity note)
    {
        var copy = Clone(note);
        _chatStickyNotes[StickyKey(note.TenantId, note.SessionId)] = copy;
        return Task.FromResult(Clone(copy));
    }

    public Task DeleteChatStickyNoteAsync(string tenantId, Guid sessionId)
    {
        _chatStickyNotes.TryRemove(StickyKey(tenantId, sessionId), out _);
        return Task.CompletedTask;
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

    public Task<TicketEventEntity> InsertTicketEventAsync(TicketEventEntity evt)
    {
        _ticketEvents[evt.Id] = Clone(evt);
        return Task.FromResult(Clone(evt));
    }

    public Task<List<TicketEventEntity>> ListTicketEventsAsync(string tenantId, Guid ticketId) =>
        Task.FromResult(_ticketEvents.Values
            .Where(e => e.TenantId == tenantId && e.TicketId == ticketId)
            .OrderBy(e => e.CreatedAt)
            .Select(Clone)
            .ToList());

    public Task<TicketStatsSnapshot> GetTicketStatsAsync(string tenantId)
    {
        var items = _tickets.Values.Where(t => t.TenantId == tenantId).ToList();
        return Task.FromResult(new TicketStatsSnapshot(
            items.Count(t => t.Status is "open" or "new"),
            items.Count(t => t.Status == "in_progress"),
            items.Count,
            items.Count(t => t.Status == "waiting")));
    }

    public Task<int> CountOpenTicketsAsync(string tenantId) =>
        Task.FromResult(_tickets.Values.Count(t =>
            t.TenantId == tenantId && t.Status is "open" or "new" or "in_progress" or "waiting"));

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

    public Task<IncidentEntity> InsertIncidentAsync(IncidentEntity incident)
    {
        _incidents[incident.Id] = Clone(incident);
        return Task.FromResult(Clone(incident));
    }

    public Task<IncidentEntity?> GetIncidentAsync(string tenantId, Guid incidentId)
    {
        _incidents.TryGetValue(incidentId, out var i);
        return Task.FromResult(i is not null && i.TenantId == tenantId ? Clone(i) : null);
    }

    public Task UpdateIncidentAsync(IncidentEntity incident)
    {
        if (_incidents.TryGetValue(incident.Id, out var existing) && existing.TenantId == incident.TenantId)
            _incidents[incident.Id] = Clone(incident);
        return Task.CompletedTask;
    }

    public Task<(List<IncidentEntity> Items, int Total)> ListIncidentsAsync(
        string tenantId, string? status, int page, int pageSize)
    {
        var q = _incidents.Values.Where(i => i.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(i => i.Status == status);
        var ordered = q.OrderByDescending(i => i.CreatedAt).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(Clone).ToList();
        return Task.FromResult((items, total));
    }

    public Task<int> NextIncidentSequenceAsync(string tenantId)
    {
        var next = _incidentSeq.AddOrUpdate(tenantId, 1, (_, n) => n + 1);
        return Task.FromResult(next);
    }

    public Task<IncidentEventEntity> InsertIncidentEventAsync(IncidentEventEntity evt)
    {
        _incidentEvents[evt.Id] = Clone(evt);
        return Task.FromResult(Clone(evt));
    }

    public Task<List<IncidentEventEntity>> ListIncidentEventsAsync(string tenantId, Guid incidentId) =>
        Task.FromResult(_incidentEvents.Values
            .Where(e => e.TenantId == tenantId && e.IncidentId == incidentId)
            .OrderBy(e => e.CreatedAt)
            .Select(Clone)
            .ToList());

    public Task<EngTaskEntity> InsertEngTaskAsync(EngTaskEntity task)
    {
        _engTasks[task.Id] = Clone(task);
        return Task.FromResult(Clone(task));
    }

    public Task<EngTaskEntity?> GetEngTaskAsync(string tenantId, Guid taskId)
    {
        _engTasks.TryGetValue(taskId, out var t);
        return Task.FromResult(t is not null && t.TenantId == tenantId ? Clone(t) : null);
    }

    public Task UpdateEngTaskAsync(EngTaskEntity task)
    {
        if (_engTasks.TryGetValue(task.Id, out var existing) && existing.TenantId == task.TenantId)
            _engTasks[task.Id] = Clone(task);
        return Task.CompletedTask;
    }

    public Task<(List<EngTaskEntity> Items, int Total)> ListEngTasksAsync(
        string tenantId, string? status, Guid? milestoneId, int page, int pageSize)
    {
        var q = _engTasks.Values.Where(t => t.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(t => t.Status == status);
        if (milestoneId.HasValue)
            q = q.Where(t => t.MilestoneId == milestoneId);
        var ordered = q.OrderByDescending(t => t.UpdatedAt).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(Clone).ToList();
        return Task.FromResult((items, total));
    }

    public Task<int> NextEngTaskSequenceAsync(string tenantId)
    {
        var next = _engTaskSeq.AddOrUpdate(tenantId, 1, (_, n) => n + 1);
        return Task.FromResult(next);
    }

    public Task<EngMilestoneEntity> InsertMilestoneAsync(EngMilestoneEntity milestone)
    {
        _milestones[milestone.Id] = Clone(milestone);
        return Task.FromResult(Clone(milestone));
    }

    public Task<EngMilestoneEntity?> GetMilestoneAsync(string tenantId, Guid milestoneId)
    {
        _milestones.TryGetValue(milestoneId, out var m);
        return Task.FromResult(m is not null && m.TenantId == tenantId ? Clone(m) : null);
    }

    public Task UpdateMilestoneAsync(EngMilestoneEntity milestone)
    {
        if (_milestones.TryGetValue(milestone.Id, out var existing) && existing.TenantId == milestone.TenantId)
            _milestones[milestone.Id] = Clone(milestone);
        return Task.CompletedTask;
    }

    public Task<List<EngMilestoneEntity>> ListMilestonesAsync(string tenantId) =>
        Task.FromResult(_milestones.Values
            .Where(m => m.TenantId == tenantId)
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.TargetDate)
            .Select(Clone)
            .ToList());

    public Task<CalendarEventEntity> UpsertCalendarEventAsync(CalendarEventEntity evt)
    {
        _calendarEvents[evt.Id] = Clone(evt);
        return Task.FromResult(Clone(evt));
    }

    public Task<CalendarEventEntity?> GetCalendarEventAsync(string tenantId, Guid eventId)
    {
        _calendarEvents.TryGetValue(eventId, out var e);
        return Task.FromResult(e is not null && e.TenantId == tenantId ? Clone(e) : null);
    }

    public Task DeleteCalendarEventAsync(string tenantId, Guid eventId)
    {
        if (_calendarEvents.TryGetValue(eventId, out var e) && e.TenantId == tenantId)
            _calendarEvents.TryRemove(eventId, out _);
        return Task.CompletedTask;
    }

    public Task<List<CalendarEventEntity>> ListCalendarEventsAsync(string tenantId) =>
        Task.FromResult(_calendarEvents.Values
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.StartsAt)
            .Select(Clone)
            .ToList());

    public Task UpsertGithubCacheAsync(GithubCacheEntity cache)
    {
        var key = $"{cache.TenantId}:{cache.CacheKind}";
        if (_githubCache.TryGetValue(key, out var existing))
            cache.Id = existing.Id;
        _githubCache[key] = Clone(cache);
        return Task.CompletedTask;
    }

    public Task<GithubCacheEntity?> GetGithubCacheAsync(string tenantId, string cacheKind)
    {
        _githubCache.TryGetValue($"{tenantId}:{cacheKind}", out var c);
        return Task.FromResult(c is null ? null : Clone(c));
    }

    public Task<TimeEntryEntity> InsertTimeEntryAsync(TimeEntryEntity entry)
    {
        _timeEntries[entry.Id] = Clone(entry);
        return Task.FromResult(Clone(entry));
    }

    public Task<TimeEntryEntity?> GetTimeEntryAsync(string tenantId, Guid id)
    {
        _timeEntries.TryGetValue(id, out var e);
        return Task.FromResult(e is not null && e.TenantId == tenantId ? Clone(e) : null);
    }

    public Task UpdateTimeEntryAsync(TimeEntryEntity entry)
    {
        if (_timeEntries.TryGetValue(entry.Id, out var existing) && existing.TenantId == entry.TenantId)
            _timeEntries[entry.Id] = Clone(entry);
        return Task.CompletedTask;
    }

    public Task<List<TimeEntryEntity>> ListTimeEntriesAsync(string tenantId, Guid? userId, string? status)
    {
        var q = _timeEntries.Values.Where(e => e.TenantId == tenantId);
        if (userId.HasValue)
            q = q.Where(e => e.UserId == userId);
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(e => e.Status == status);
        return Task.FromResult(q.OrderByDescending(e => e.CreatedAt).Select(Clone).ToList());
    }

    public Task<TimeEntryEntity?> GetOpenClockAsync(string tenantId, Guid? userId)
    {
        var hit = _timeEntries.Values
            .Where(e => e.TenantId == tenantId
                        && e.EntryType == "clock"
                        && e.Status == "open"
                        && (!userId.HasValue || e.UserId == userId))
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefault();
        return Task.FromResult(hit is null ? null : Clone(hit));
    }

    public Task<ApprovalRequestEntity> InsertApprovalAsync(ApprovalRequestEntity request)
    {
        _approvals[request.Id] = Clone(request);
        return Task.FromResult(Clone(request));
    }

    public Task<ApprovalRequestEntity?> GetApprovalAsync(string tenantId, Guid id)
    {
        _approvals.TryGetValue(id, out var a);
        return Task.FromResult(a is not null && a.TenantId == tenantId ? Clone(a) : null);
    }

    public Task UpdateApprovalAsync(ApprovalRequestEntity request)
    {
        if (_approvals.TryGetValue(request.Id, out var existing) && existing.TenantId == request.TenantId)
            _approvals[request.Id] = Clone(request);
        return Task.CompletedTask;
    }

    public Task<List<ApprovalRequestEntity>> ListApprovalsAsync(string tenantId, string? status)
    {
        var q = _approvals.Values.Where(a => a.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(a => a.Status == status);
        return Task.FromResult(q.OrderByDescending(a => a.CreatedAt).Select(Clone).ToList());
    }

    public Task<PayRateEntity> InsertPayRateAsync(PayRateEntity rate)
    {
        _payRates[rate.Id] = Clone(rate);
        return Task.FromResult(Clone(rate));
    }

    public Task<List<PayRateEntity>> ListPayRatesAsync(string tenantId) =>
        Task.FromResult(_payRates.Values
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.EffectiveFrom)
            .ThenByDescending(r => r.CreatedAt)
            .Select(Clone)
            .ToList());

    public Task<PayPeriodEntity> InsertPayPeriodAsync(PayPeriodEntity period)
    {
        _payPeriods[period.Id] = Clone(period);
        return Task.FromResult(Clone(period));
    }

    public Task<PayPeriodEntity?> GetPayPeriodAsync(string tenantId, Guid id)
    {
        _payPeriods.TryGetValue(id, out var p);
        return Task.FromResult(p is not null && p.TenantId == tenantId ? Clone(p) : null);
    }

    public Task UpdatePayPeriodAsync(PayPeriodEntity period)
    {
        if (_payPeriods.TryGetValue(period.Id, out var existing) && existing.TenantId == period.TenantId)
            _payPeriods[period.Id] = Clone(period);
        return Task.CompletedTask;
    }

    public Task<List<PayPeriodEntity>> ListPayPeriodsAsync(string tenantId) =>
        Task.FromResult(_payPeriods.Values
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.StartsOn)
            .Select(Clone)
            .ToList());

    public Task InsertAuditEventAsync(AuditEventEntity evt)
    {
        _audit[evt.Id] = Clone(evt);
        return Task.CompletedTask;
    }

    public Task<List<AuditEventEntity>> ListAuditEventsAsync(
        string tenantId, string? action, string? entityType, string? search, int take)
    {
        var q = _audit.Values.Where(e => e.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(action))
            q = q.Where(e => e.Action == action);
        if (!string.IsNullOrWhiteSpace(entityType))
            q = q.Where(e => e.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(e =>
                (e.EntityId?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                || (e.ActorName?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                || e.Action.Contains(s, StringComparison.OrdinalIgnoreCase)
                || (e.AfterJson?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        return Task.FromResult(q.OrderByDescending(e => e.CreatedAt).Take(take).Select(Clone).ToList());
    }

    public Task InsertNotificationAsync(NotificationEntity n)
    {
        _notifications[n.Id] = Clone(n);
        return Task.CompletedTask;
    }

    public Task<NotificationEntity?> GetNotificationAsync(string tenantId, Guid id)
    {
        _notifications.TryGetValue(id, out var n);
        return Task.FromResult(n is not null && n.TenantId == tenantId ? Clone(n) : null);
    }

    public Task UpdateNotificationAsync(NotificationEntity n)
    {
        if (_notifications.TryGetValue(n.Id, out var existing) && existing.TenantId == n.TenantId)
            _notifications[n.Id] = Clone(n);
        return Task.CompletedTask;
    }

    public Task<List<NotificationEntity>> ListNotificationsAsync(string tenantId, Guid? userId, bool unreadOnly)
    {
        var q = _notifications.Values.Where(n => n.TenantId == tenantId);
        if (userId.HasValue)
            q = q.Where(n => n.UserId == null || n.UserId == userId);
        if (unreadOnly)
            q = q.Where(n => n.ReadAt is null);
        return Task.FromResult(q.OrderByDescending(n => n.CreatedAt).Select(Clone).ToList());
    }

    public Task<NotificationPrefsEntity?> GetNotificationPrefsAsync(string tenantId, Guid userId)
    {
        _notifPrefs.TryGetValue($"{tenantId}:{userId}", out var p);
        return Task.FromResult(p is null ? null : Clone(p));
    }

    public Task UpsertNotificationPrefsAsync(NotificationPrefsEntity prefs)
    {
        var key = $"{prefs.TenantId}:{prefs.UserId}";
        if (_notifPrefs.TryGetValue(key, out var existing))
            prefs.Id = existing.Id;
        _notifPrefs[key] = Clone(prefs);
        return Task.CompletedTask;
    }

    public Task InsertOpsFileAsync(OpsFileEntity file)
    {
        _opsFiles[file.Id] = Clone(file);
        return Task.CompletedTask;
    }

    public Task DeleteOpsFileAsync(string tenantId, Guid id)
    {
        if (_opsFiles.TryGetValue(id, out var f) && f.TenantId == tenantId)
            _opsFiles.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<List<OpsFileEntity>> ListOpsFilesAsync(string tenantId, string? folderPath)
    {
        var q = _opsFiles.Values.Where(f => f.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(folderPath))
            q = q.Where(f => f.FolderPath == folderPath);
        return Task.FromResult(q.OrderByDescending(f => f.CreatedAt).Select(Clone).ToList());
    }

    public Task<IntegrationEntity> UpsertIntegrationAsync(IntegrationEntity integration)
    {
        var key = $"{integration.TenantId}:{integration.Provider}";
        if (_integrations.TryGetValue(key, out var existing))
            integration.Id = existing.Id;
        _integrations[key] = Clone(integration);
        return Task.FromResult(Clone(integration));
    }

    public Task<IntegrationEntity?> GetIntegrationAsync(string tenantId, string provider)
    {
        _integrations.TryGetValue($"{tenantId}:{provider}", out var i);
        return Task.FromResult(i is null ? null : Clone(i));
    }

    public Task<List<IntegrationEntity>> ListIntegrationsAsync(string tenantId) =>
        Task.FromResult(_integrations.Values
            .Where(i => i.TenantId == tenantId)
            .Select(Clone)
            .ToList());

    public Task UpsertIntegrationSecretAsync(IntegrationSecretEntity secret)
    {
        var key = $"{secret.TenantId}:{secret.Provider}:{secret.SecretKey}";
        if (_integrationSecrets.TryGetValue(key, out var existing))
            secret.Id = existing.Id;
        _integrationSecrets[key] = Clone(secret);
        return Task.CompletedTask;
    }

    public Task DeleteIntegrationSecretsAsync(string tenantId, string provider)
    {
        foreach (var key in _integrationSecrets.Keys.Where(k => k.StartsWith($"{tenantId}:{provider}:")).ToList())
            _integrationSecrets.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<List<IntegrationSecretEntity>> ListIntegrationSecretsAsync(string tenantId, string provider) =>
        Task.FromResult(_integrationSecrets.Values
            .Where(s => s.TenantId == tenantId && s.Provider == provider)
            .Select(Clone)
            .ToList());

    public Task<List<PlatformHelpArticleEntity>> ListPlatformHelpAsync(bool publishedOnly)
    {
        SeedPlatformHelpIfEmpty();
        var q = _platformHelp.Values.AsEnumerable();
        if (publishedOnly)
            q = q.Where(a => a.Status == "published");
        return Task.FromResult(q.OrderBy(a => a.SortOrder).ThenBy(a => a.Title).Select(Clone).ToList());
    }

    public Task<PlatformHelpArticleEntity?> GetPlatformHelpBySlugAsync(string slug)
    {
        SeedPlatformHelpIfEmpty();
        var hit = _platformHelp.Values.FirstOrDefault(a =>
            string.Equals(a.Slug, slug, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(hit is null ? null : Clone(hit));
    }

    public Task<PlatformHelpArticleEntity> UpsertPlatformHelpAsync(PlatformHelpArticleEntity article)
    {
        var existing = _platformHelp.Values.FirstOrDefault(a =>
            string.Equals(a.Slug, article.Slug, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            article.Id = existing.Id;
        _platformHelp[article.Id] = Clone(article);
        return Task.FromResult(Clone(article));
    }

    private void SeedPlatformHelpIfEmpty()
    {
        if (!_platformHelp.IsEmpty) return;
        var now = DateTime.UtcNow;
        void Add(string slug, string title, string body, string cat, int order)
        {
            var id = Guid.NewGuid();
            _platformHelp[id] = new PlatformHelpArticleEntity
            {
                Id = id, Slug = slug, Title = title, Body = body, Category = cat,
                Status = "published", SortOrder = order, CreatedAt = now, UpdatedAt = now
            };
        }
        Add("getting-started", "Getting started with KobNeti",
            "Use the product switcher to pick a brand, then open Support, Engineering, or People Ops.", "general", 1);
        Add("support-hub", "Support Hub",
            "Live chat, tickets, incidents, and product KB live under Support Hub.", "support", 2);
        Add("time-payroll", "Time & payroll",
            "Clock in/out under Time & Approvals. Payroll runs use approved time only.", "people", 3);
    }

    public Task InsertReportRunAsync(ReportRunEntity run)
    {
        _reportRuns[run.Id] = Clone(run);
        return Task.CompletedTask;
    }

    public Task<ReportRunEntity?> GetReportRunAsync(string tenantId, Guid id)
    {
        _reportRuns.TryGetValue(id, out var r);
        return Task.FromResult(r is not null && r.TenantId == tenantId ? Clone(r) : null);
    }

    public Task<List<ReportRunEntity>> ListReportRunsAsync(string tenantId) =>
        Task.FromResult(_reportRuns.Values
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(Clone)
            .ToList());

    public Task<ImChannelEntity> InsertImChannelAsync(ImChannelEntity channel)
    {
        _imChannels[channel.Id] = Clone(channel);
        return Task.FromResult(Clone(channel));
    }

    public Task<ImChannelEntity?> GetImChannelAsync(string tenantId, Guid id)
    {
        _imChannels.TryGetValue(id, out var c);
        return Task.FromResult(c is not null && c.TenantId == tenantId ? Clone(c) : null);
    }

    public Task<List<ImChannelEntity>> ListImChannelsAsync(string tenantId) =>
        Task.FromResult(_imChannels.Values
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.Name)
            .Select(Clone)
            .ToList());

    public Task InsertImMessageAsync(ImMessageEntity message)
    {
        _imMessages[message.Id] = Clone(message);
        return Task.CompletedTask;
    }

    public Task<List<ImMessageEntity>> ListImMessagesAsync(string tenantId, Guid channelId) =>
        Task.FromResult(_imMessages.Values
            .Where(m => m.TenantId == tenantId && m.ChannelId == channelId)
            .OrderBy(m => m.CreatedAt)
            .Select(Clone)
            .ToList());

    public Task InsertAssetAsync(AssetEntity asset)
    {
        _assets[asset.Id] = Clone(asset);
        return Task.CompletedTask;
    }

    public Task UpdateAssetAsync(AssetEntity asset)
    {
        if (_assets.TryGetValue(asset.Id, out var existing) && existing.TenantId == asset.TenantId)
            _assets[asset.Id] = Clone(asset);
        return Task.CompletedTask;
    }

    public Task<AssetEntity?> GetAssetAsync(string tenantId, Guid id)
    {
        _assets.TryGetValue(id, out var a);
        return Task.FromResult(a is not null && a.TenantId == tenantId ? Clone(a) : null);
    }

    public Task<List<AssetEntity>> ListAssetsAsync(string tenantId) =>
        Task.FromResult(_assets.Values
            .Where(a => a.TenantId == tenantId)
            .OrderBy(a => a.Name)
            .Select(Clone)
            .ToList());

    public Task<List<AssetEntity>> ListAssetsRenewingSoonAsync(string tenantId, DateOnly before) =>
        Task.FromResult(_assets.Values
            .Where(a => a.TenantId == tenantId && a.RenewalDate.HasValue && a.RenewalDate <= before && a.Status != "retired")
            .Select(Clone)
            .ToList());

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
