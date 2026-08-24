using KobNeti.Api.Data;
using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;

namespace KobNeti.Api.Services;

public interface IEngTaskService
{
    Task<Response<List<EngTaskDTO>>> ListAsync(string tenantId, string? status, Guid? milestoneId, int page, int pageSize);
    Task<Response<EngTaskDTO>> GetAsync(string tenantId, Guid id);
    Task<Response<EngTaskDTO>> CreateAsync(string tenantId, CreateEngTaskDTO request, string? actorName, Guid? actorUserId);
    Task<Response<EngTaskDTO>> UpdateAsync(string tenantId, Guid id, UpdateEngTaskDTO request);
}

public class EngTaskService : IEngTaskService
{
    private readonly ISupportStore _store;

    public EngTaskService(ISupportStore store) => _store = store;

    public async Task<Response<List<EngTaskDTO>>> ListAsync(
        string tenantId, string? status, Guid? milestoneId, int page, int pageSize)
    {
        var (items, _) = await _store.ListEngTasksAsync(tenantId, status, milestoneId, page, pageSize);
        return Response<List<EngTaskDTO>>.SuccessResponse(items.Select(Map).ToList(), "Tasks loaded");
    }

    public async Task<Response<EngTaskDTO>> GetAsync(string tenantId, Guid id)
    {
        var task = await _store.GetEngTaskAsync(tenantId, id);
        if (task is null)
            return Response<EngTaskDTO>.Fail("Task not found");
        return Response<EngTaskDTO>.SuccessResponse(Map(task), "Task loaded");
    }

    public async Task<Response<EngTaskDTO>> CreateAsync(
        string tenantId, CreateEngTaskDTO request, string? actorName, Guid? actorUserId)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Response<EngTaskDTO>.Fail("Title is required");

        var type = (request.TaskType ?? EngTaskType.Feature).Trim().ToLowerInvariant();
        if (!EngTaskType.All.Contains(type))
            return Response<EngTaskDTO>.Fail("Invalid task type");

        var status = (request.Status ?? EngTaskStatus.Backlog).Trim().ToLowerInvariant();
        if (!EngTaskStatus.All.Contains(status))
            return Response<EngTaskDTO>.Fail("Invalid status");

        var priority = (request.Priority ?? EngTaskPriority.Medium).Trim().ToLowerInvariant();
        if (!EngTaskPriority.All.Contains(priority))
            return Response<EngTaskDTO>.Fail("Invalid priority");

        if (request.TicketId.HasValue)
        {
            var ticket = await _store.GetTicketAsync(tenantId, request.TicketId.Value);
            if (ticket is null)
                return Response<EngTaskDTO>.Fail("Ticket not found");
        }

        if (request.MilestoneId.HasValue)
        {
            var ms = await _store.GetMilestoneAsync(tenantId, request.MilestoneId.Value);
            if (ms is null)
                return Response<EngTaskDTO>.Fail("Milestone not found");
        }

        var now = DateTime.UtcNow;
        var seq = await _store.NextEngTaskSequenceAsync(tenantId);
        var task = new EngTaskEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TaskNumber = $"TASK-{now:yyyyMMdd}-{seq:D4}",
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            TaskType = type,
            Status = status,
            Priority = priority,
            EstimatePoints = request.EstimatePoints,
            AssigneeName = string.IsNullOrWhiteSpace(request.AssigneeName) ? actorName : request.AssigneeName.Trim(),
            AssigneeUserId = actorUserId,
            TicketId = request.TicketId,
            MilestoneId = request.MilestoneId,
            GithubPrUrl = NormalizeUrl(request.GithubPrUrl),
            CreatedAt = now,
            UpdatedAt = now
        };
        await _store.InsertEngTaskAsync(task);

        if (task.TicketId.HasValue)
            await LinkTicketAsync(tenantId, task.TicketId.Value, task.Id);

        return Response<EngTaskDTO>.SuccessResponse(Map(task), "Task created");
    }

    public async Task<Response<EngTaskDTO>> UpdateAsync(string tenantId, Guid id, UpdateEngTaskDTO request)
    {
        var task = await _store.GetEngTaskAsync(tenantId, id);
        if (task is null)
            return Response<EngTaskDTO>.Fail("Task not found");

        if (!string.IsNullOrWhiteSpace(request.Title))
            task.Title = request.Title.Trim();
        if (request.Description != null)
            task.Description = request.Description;

        if (!string.IsNullOrWhiteSpace(request.TaskType))
        {
            var type = request.TaskType.Trim().ToLowerInvariant();
            if (!EngTaskType.All.Contains(type))
                return Response<EngTaskDTO>.Fail("Invalid task type");
            task.TaskType = type;
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToLowerInvariant();
            if (!EngTaskStatus.All.Contains(status))
                return Response<EngTaskDTO>.Fail("Invalid status");
            task.Status = status;
        }

        if (!string.IsNullOrWhiteSpace(request.Priority))
        {
            var priority = request.Priority.Trim().ToLowerInvariant();
            if (!EngTaskPriority.All.Contains(priority))
                return Response<EngTaskDTO>.Fail("Invalid priority");
            task.Priority = priority;
        }

        if (request.EstimatePoints.HasValue)
            task.EstimatePoints = request.EstimatePoints;
        if (request.AssigneeName != null)
            task.AssigneeName = string.IsNullOrWhiteSpace(request.AssigneeName) ? null : request.AssigneeName.Trim();

        if (request.ClearTicketId)
            task.TicketId = null;
        else if (request.TicketId.HasValue)
        {
            var ticket = await _store.GetTicketAsync(tenantId, request.TicketId.Value);
            if (ticket is null)
                return Response<EngTaskDTO>.Fail("Ticket not found");
            task.TicketId = request.TicketId;
        }

        if (request.ClearMilestoneId)
            task.MilestoneId = null;
        else if (request.MilestoneId.HasValue)
        {
            var ms = await _store.GetMilestoneAsync(tenantId, request.MilestoneId.Value);
            if (ms is null)
                return Response<EngTaskDTO>.Fail("Milestone not found");
            task.MilestoneId = request.MilestoneId;
        }

        if (request.ClearGithubPrUrl)
            task.GithubPrUrl = null;
        else if (request.GithubPrUrl != null)
            task.GithubPrUrl = NormalizeUrl(request.GithubPrUrl);

        task.UpdatedAt = DateTime.UtcNow;
        await _store.UpdateEngTaskAsync(task);

        if (task.TicketId.HasValue)
            await LinkTicketAsync(tenantId, task.TicketId.Value, task.Id);

        return Response<EngTaskDTO>.SuccessResponse(Map(task), "Task updated");
    }

    private async Task LinkTicketAsync(string tenantId, Guid ticketId, Guid taskId)
    {
        var ticket = await _store.GetTicketAsync(tenantId, ticketId);
        if (ticket is null)
            return;
        if (ticket.EngTaskId == taskId)
            return;
        ticket.EngTaskId = taskId;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _store.UpdateTicketAsync(ticket);
        await _store.InsertTicketEventAsync(new TicketEventEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TicketId = ticketId,
            EventType = "eng_task_linked",
            Detail = $"Linked to eng task {taskId}",
            CreatedAt = DateTime.UtcNow
        });
    }

    private static string? NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        var trimmed = url.Trim();
        return Uri.TryCreate(trimmed, UriKind.Absolute, out _) ? trimmed : null;
    }

    private static EngTaskDTO Map(EngTaskEntity t) => new()
    {
        Id = t.Id,
        TaskNumber = t.TaskNumber,
        Title = t.Title,
        Description = t.Description,
        TaskType = t.TaskType,
        Status = t.Status,
        Priority = t.Priority,
        EstimatePoints = t.EstimatePoints,
        AssigneeName = t.AssigneeName,
        AssigneeUserId = t.AssigneeUserId,
        TicketId = t.TicketId,
        MilestoneId = t.MilestoneId,
        GithubPrUrl = t.GithubPrUrl,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };
}
