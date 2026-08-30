using KobNeti.Api.Data;
using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;

namespace KobNeti.Api.Services;

/// <summary>W3.3 — stub notifications until Module 17. Logs only.</summary>
public interface INotificationStub
{
    Task NotifyAsync(string tenantId, string channel, string subject, string body, CancellationToken ct = default);
}

public class LoggingNotificationStub : INotificationStub
{
    private readonly ILogger<LoggingNotificationStub> _logger;

    public LoggingNotificationStub(ILogger<LoggingNotificationStub> logger) => _logger = logger;

    public Task NotifyAsync(string tenantId, string channel, string subject, string body, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Notify stub [{Channel}] tenant={Tenant} subject={Subject} body={Body}",
            channel, tenantId, subject, body);
        return Task.CompletedTask;
    }
}

public interface IIncidentService
{
    Task<Response<List<IncidentDTO>>> ListAsync(string tenantId, string? status, int page, int pageSize);
    Task<Response<IncidentDTO>> GetAsync(string tenantId, Guid id);
    Task<Response<IncidentDTO>> CreateAsync(string tenantId, CreateIncidentDTO request, string? actorName, Guid? actorUserId);
    Task<Response<IncidentDTO>> EscalateFromTicketAsync(string tenantId, Guid ticketId, EscalateTicketDTO request, string? actorName, Guid? actorUserId);
    Task<Response<IncidentDTO>> EscalateFromChatAsync(string tenantId, Guid sessionId, EscalateFromChatDTO request, string? actorName, Guid? actorUserId);
    Task<Response<IncidentDTO>> UpdateAsync(string tenantId, Guid id, UpdateIncidentDTO request, string? actorName);
}

public class IncidentService : IIncidentService
{
    private readonly ISupportStore _store;
    private readonly INotificationStub _notify;
    private readonly ILogger<IncidentService> _logger;
    private readonly IAuditService _audit;

    public IncidentService(ISupportStore store, INotificationStub notify, ILogger<IncidentService> logger, IAuditService audit)
    {
        _store = store;
        _notify = notify;
        _logger = logger;
        _audit = audit;
    }

    public async Task<Response<List<IncidentDTO>>> ListAsync(
        string tenantId, string? status, int page, int pageSize)
    {
        var (items, _) = await _store.ListIncidentsAsync(tenantId, status, page, pageSize);
        var list = new List<IncidentDTO>();
        foreach (var i in items)
            list.Add(await MapAsync(tenantId, i, includeTimeline: false));
        return Response<List<IncidentDTO>>.SuccessResponse(list, "Incidents loaded");
    }

    public async Task<Response<IncidentDTO>> GetAsync(string tenantId, Guid id)
    {
        var incident = await _store.GetIncidentAsync(tenantId, id);
        if (incident is null)
            return Response<IncidentDTO>.Fail("Incident not found");
        return Response<IncidentDTO>.SuccessResponse(await MapAsync(tenantId, incident, true), "Incident loaded");
    }

    public async Task<Response<IncidentDTO>> CreateAsync(
        string tenantId, CreateIncidentDTO request, string? actorName, Guid? actorUserId)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Response<IncidentDTO>.Fail("Title is required");

        var severity = (request.Severity ?? IncidentSeverity.Sev3).Trim().ToLowerInvariant();
        if (!IncidentSeverity.All.Contains(severity))
            return Response<IncidentDTO>.Fail("Invalid severity");

        if (request.SourceTicketId.HasValue)
        {
            var ticket = await _store.GetTicketAsync(tenantId, request.SourceTicketId.Value);
            if (ticket is null)
                return Response<IncidentDTO>.Fail("Source ticket not found");
        }

        var now = DateTime.UtcNow;
        var seq = await _store.NextIncidentSequenceAsync(tenantId);
        var incident = new IncidentEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IncidentNumber = $"INC-{now:yyyyMMdd}-{seq:D4}",
            Title = request.Title.Trim(),
            Severity = severity,
            Status = IncidentStatus.Open,
            CommanderName = string.IsNullOrWhiteSpace(request.CommanderName) ? actorName : request.CommanderName.Trim(),
            CommanderUserId = actorUserId,
            SourceTicketId = request.SourceTicketId,
            SourceChatSessionId = request.SourceChatSessionId,
            CreatedAt = now,
            UpdatedAt = now
        };
        incident = await _store.InsertIncidentAsync(incident);
        await AddEventAsync(tenantId, incident.Id, "created", actorName, $"Severity {severity}");
        await NotifyAssigneesAsync(tenantId, incident, "Incident created");
        return Response<IncidentDTO>.SuccessResponse(await MapAsync(tenantId, incident, true), "Incident created");
    }

    public async Task<Response<IncidentDTO>> EscalateFromTicketAsync(
        string tenantId, Guid ticketId, EscalateTicketDTO request, string? actorName, Guid? actorUserId)
    {
        var ticket = await _store.GetTicketAsync(tenantId, ticketId);
        if (ticket is null)
            return Response<IncidentDTO>.Fail("Ticket not found");

        var title = string.IsNullOrWhiteSpace(request.Title)
            ? $"Escalation: {ticket.Subject}"
            : request.Title.Trim();

        var created = await CreateAsync(tenantId, new CreateIncidentDTO
        {
            Title = title,
            Severity = request.Severity,
            CommanderName = request.CommanderName,
            SourceTicketId = ticketId
        }, actorName, actorUserId);

        if (!created.Success || created.Data is null)
            return created;

        await AddEventAsync(tenantId, created.Data.Id, "escalated_from_ticket", actorName,
            $"From ticket {ticket.TicketNumber}");
        await _store.InsertTicketEventAsync(new TicketEventEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TicketId = ticketId,
            EventType = "escalated",
            ActorName = actorName,
            Detail = $"Escalated to {created.Data.IncidentNumber}",
            CreatedAt = DateTime.UtcNow
        });

        if (TicketStatus.IsNewOrOpen(ticket.Status) || ticket.Status == TicketStatus.Waiting)
        {
            ticket.Status = TicketStatus.InProgress;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _store.UpdateTicketAsync(ticket);
        }

        await _audit.WriteAsync(tenantId, AuditActions.IncidentEscalate, "incident", created.Data.Id.ToString(),
            actorUserId, actorName, null, new { ticketId, created.Data.IncidentNumber });

        return await GetAsync(tenantId, created.Data.Id);
    }

    public async Task<Response<IncidentDTO>> EscalateFromChatAsync(
        string tenantId, Guid sessionId, EscalateFromChatDTO request, string? actorName, Guid? actorUserId)
    {
        var session = await _store.GetSessionAsync(tenantId, sessionId);
        if (session is null)
            return Response<IncidentDTO>.Fail("Chat session not found");

        var messages = await _store.ListMessagesAsync(tenantId, sessionId);
        var customerName = string.IsNullOrWhiteSpace(session.GuestName) ? "Chat visitor" : session.GuestName.Trim();
        return await EscalateFromChatCoreAsync(
            tenantId,
            sessionId,
            customerName,
            messages.Select(m => (m.SenderType, (string?)m.SenderName, m.Message)),
            request,
            actorName,
            actorUserId);
    }

    /// <summary>
    /// Creates an ops-owned incident from a bridged product-API chat session.
    /// </summary>
    public Task<Response<IncidentDTO>> EscalateFromChatAsync(
        string tenantId,
        Guid sessionId,
        ChatSessionDTO session,
        IReadOnlyList<ChatMessageDTO> messages,
        EscalateFromChatDTO request,
        string? actorName,
        Guid? actorUserId)
    {
        var customerName = string.IsNullOrWhiteSpace(session.CustomerName) ? "Chat visitor" : session.CustomerName.Trim();
        return EscalateFromChatCoreAsync(
            tenantId,
            sessionId,
            customerName,
            messages.Select(m => (m.SenderType, (string?)m.SenderName, m.Message)),
            request,
            actorName,
            actorUserId);
    }

    private async Task<Response<IncidentDTO>> EscalateFromChatCoreAsync(
        string tenantId,
        Guid sessionId,
        string customerName,
        IEnumerable<(string SenderType, string? SenderName, string Message)> messages,
        EscalateFromChatDTO request,
        string? actorName,
        Guid? actorUserId)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Response<IncidentDTO>.Fail("Reason for escalation is required");

        var target = NormalizeEscalationTarget(request.Target);
        var targetLabel = GetEscalationTargetLabel(target, request.AssigneeName);
        var incidentSeverity = MapUiSeverityToIncident(request.Severity);
        var title = string.IsNullOrWhiteSpace(request.Title)
            ? $"Escalation: {customerName} → {targetLabel}"
            : request.Title.Trim();

        var commanderName = target switch
        {
            "agent" => string.IsNullOrWhiteSpace(request.AssigneeName) ? actorName : request.AssigneeName.Trim(),
            "support_manager" => "Support Manager",
            _ => actorName
        };
        var commanderUserId = target == "agent" ? request.AssigneeUserId : actorUserId;

        var transcript = string.Join(
            "\n",
            messages
                .TakeLast(30)
                .Select(m => $"[{m.SenderType}] {m.SenderName ?? "Unknown"}: {m.Message}"));

        var postmortem = $"Reason: {request.Reason.Trim()}\nTarget: {targetLabel}\nSeverity: {request.Severity.Trim()}";
        if (!string.IsNullOrWhiteSpace(transcript))
            postmortem += $"\n\nChat transcript:\n{transcript}";

        var now = DateTime.UtcNow;
        var seq = await _store.NextIncidentSequenceAsync(tenantId);
        var incident = new IncidentEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IncidentNumber = $"INC-{now:yyyyMMdd}-{seq:D4}",
            Title = title.Length > 240 ? title[..240] : title,
            Severity = incidentSeverity,
            Status = IncidentStatus.Open,
            CommanderName = commanderName,
            CommanderUserId = commanderUserId,
            SourceChatSessionId = sessionId,
            PostmortemNotes = postmortem,
            CreatedAt = now,
            UpdatedAt = now
        };

        incident = await _store.InsertIncidentAsync(incident);
        await AddEventAsync(tenantId, incident.Id, "created", actorName, $"Severity {incidentSeverity}");
        await AddEventAsync(
            tenantId,
            incident.Id,
            "escalated_from_chat",
            actorName,
            $"Target: {targetLabel}; Reason: {request.Reason.Trim()}");
        await NotifyAssigneesAsync(tenantId, incident, "Chat escalation");
        await _audit.WriteAsync(
            tenantId,
            AuditActions.IncidentEscalate,
            "incident",
            incident.Id.ToString(),
            actorUserId,
            actorName,
            null,
            new { sessionId, incident.IncidentNumber, target, request.Severity });

        return Response<IncidentDTO>.SuccessResponse(await MapAsync(tenantId, incident, true), "Incident created from chat");
    }

    private static string NormalizeEscalationTarget(string? target)
    {
        var value = (target ?? "engineering").Trim().ToLowerInvariant();
        return value switch
        {
            "support_manager" or "support-manager" or "manager" => "support_manager",
            "agent" or "specific_agent" => "agent",
            _ => "engineering"
        };
    }

    private static string GetEscalationTargetLabel(string target, string? assigneeName) =>
        target switch
        {
            "support_manager" => "Support Team (Manager)",
            "agent" => string.IsNullOrWhiteSpace(assigneeName)
                ? "Specific Agent"
                : $"Agent: {assigneeName.Trim()}",
            _ => "Engineering Team"
        };

    private static string MapUiSeverityToIncident(string? severity)
    {
        var value = (severity ?? "High").Trim();
        return value.ToLowerInvariant() switch
        {
            "urgent" => IncidentSeverity.Sev1,
            "high" => IncidentSeverity.Sev2,
            "medium" => IncidentSeverity.Sev3,
            "low" => IncidentSeverity.Sev4,
            IncidentSeverity.Sev1 or IncidentSeverity.Sev2 or IncidentSeverity.Sev3 or IncidentSeverity.Sev4 => value.ToLowerInvariant(),
            _ => IncidentSeverity.Sev2
        };
    }

    public async Task<Response<IncidentDTO>> UpdateAsync(
        string tenantId, Guid id, UpdateIncidentDTO request, string? actorName)
    {
        var incident = await _store.GetIncidentAsync(tenantId, id);
        if (incident is null)
            return Response<IncidentDTO>.Fail("Incident not found");

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToLowerInvariant();
            if (!IncidentStatus.All.Contains(status))
                return Response<IncidentDTO>.Fail("Invalid status");
            incident.Status = status;
            if (status is IncidentStatus.Resolved or IncidentStatus.Closed)
                incident.ResolvedAt ??= DateTime.UtcNow;
            await AddEventAsync(tenantId, incident.Id, "status_changed", actorName, $"Status → {status}");
        }

        if (!string.IsNullOrWhiteSpace(request.Severity))
        {
            var severity = request.Severity.Trim().ToLowerInvariant();
            if (!IncidentSeverity.All.Contains(severity))
                return Response<IncidentDTO>.Fail("Invalid severity");
            incident.Severity = severity;
            await AddEventAsync(tenantId, incident.Id, "severity_changed", actorName, $"Severity → {severity}");
        }

        if (request.CommanderName != null)
            incident.CommanderName = string.IsNullOrWhiteSpace(request.CommanderName)
                ? null
                : request.CommanderName.Trim();

        if (request.PostmortemNotes != null)
        {
            incident.PostmortemNotes = request.PostmortemNotes;
            await AddEventAsync(tenantId, incident.Id, "postmortem_updated", actorName, "Postmortem notes updated");
        }

        incident.UpdatedAt = DateTime.UtcNow;
        await _store.UpdateIncidentAsync(incident);
        await NotifyAssigneesAsync(tenantId, incident, "Incident updated");
        return Response<IncidentDTO>.SuccessResponse(await MapAsync(tenantId, incident, true), "Incident updated");
    }

    private async Task NotifyAssigneesAsync(string tenantId, IncidentEntity incident, string subject)
    {
        try
        {
            var who = incident.CommanderName ?? "unassigned";
            await _notify.NotifyAsync(
                tenantId,
                "incident",
                $"{subject}: {incident.IncidentNumber}",
                $"Commander={who}; severity={incident.Severity}; status={incident.Status}; title={incident.Title}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Incident notify stub failed for {Incident}", incident.IncidentNumber);
        }
    }

    private async Task AddEventAsync(string tenantId, Guid incidentId, string type, string? actor, string? detail)
    {
        await _store.InsertIncidentEventAsync(new IncidentEventEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IncidentId = incidentId,
            EventType = type,
            ActorName = actor,
            Detail = detail,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task<IncidentDTO> MapAsync(string tenantId, IncidentEntity i, bool includeTimeline)
    {
        var events = includeTimeline
            ? await _store.ListIncidentEventsAsync(tenantId, i.Id)
            : [];

        return new IncidentDTO
        {
            Id = i.Id,
            IncidentNumber = i.IncidentNumber,
            Title = i.Title,
            Severity = i.Severity,
            Status = i.Status,
            CommanderName = i.CommanderName,
            CommanderUserId = i.CommanderUserId,
            SourceTicketId = i.SourceTicketId,
            SourceChatSessionId = i.SourceChatSessionId,
            PostmortemNotes = i.PostmortemNotes,
            CreatedAt = i.CreatedAt,
            UpdatedAt = i.UpdatedAt,
            ResolvedAt = i.ResolvedAt,
            Timeline = events.Select(e => new IncidentEventDTO
            {
                Id = e.Id,
                EventType = e.EventType,
                ActorName = e.ActorName,
                Detail = e.Detail,
                CreatedAt = e.CreatedAt
            }).ToList()
        };
    }
}
