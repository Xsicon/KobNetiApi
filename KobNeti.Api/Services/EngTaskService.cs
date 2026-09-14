using KobNeti.Api.Data;
using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;
using KobNeti.Api.Staff;

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
    private readonly IStaffDirectory _staff;

    public EngTaskService(ISupportStore store, IStaffDirectory staff)
    {
        _store = store;
        _staff = staff;
    }

    public async Task<Response<List<EngTaskDTO>>> ListAsync(
        string tenantId, string? status, Guid? milestoneId, int page, int pageSize)
    {
        await EngineeringSampleData.EnsureSeededAsync(_store, tenantId, _staff);
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
            AssigneeName = Members.Join(CreateMembers(request, actorName)),
            AssigneeUserId = actorUserId,
            TicketId = request.TicketId,
            MilestoneId = request.MilestoneId,
            GithubPrUrl = NormalizeUrl(request.GithubPrUrl),
            CreatedAt = now,
            UpdatedAt = now
        };
        var saved = await _store.InsertEngTaskAsync(task);

        if (saved.TicketId.HasValue)
            await LinkTicketAsync(tenantId, saved.TicketId.Value, saved.Id);

        return Response<EngTaskDTO>.SuccessResponse(Map(saved), "Task created");
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
        if (request.AssigneeNames != null)
            task.AssigneeName = Members.Join(request.AssigneeNames);
        else if (request.AssigneeName != null)
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
        AssigneeName = Members.Primary(t.AssigneeName),
        AssigneeUserId = t.AssigneeUserId,
        AssigneeNames = Members.Parse(t.AssigneeName),
        TicketId = t.TicketId,
        MilestoneId = t.MilestoneId,
        GithubPrUrl = t.GithubPrUrl,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };

    private static IEnumerable<string> CreateMembers(CreateEngTaskDTO request, string? actorName)
    {
        if (request.AssigneeNames is { Count: > 0 })
            return request.AssigneeNames;
        if (!string.IsNullOrWhiteSpace(request.AssigneeName))
            return [request.AssigneeName];
        if (!string.IsNullOrWhiteSpace(actorName))
            return [actorName];
        return [];
    }

    private static class Members
    {
        public static List<string> Parse(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return [];

            var trimmed = raw.Trim();
            if (trimmed.StartsWith('['))
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Deserialize<List<string>>(trimmed);
                    if (json is { Count: > 0 })
                        return Distinct(json);
                }
                catch (System.Text.Json.JsonException)
                {
                    // Fall through to delimiter parsing.
                }
            }

            return Distinct(trimmed.Split(['\n', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        public static string? Join(IEnumerable<string>? names)
        {
            var list = Distinct(names ?? []);
            return list.Count == 0 ? null : string.Join('\n', list);
        }

        public static string? Primary(string? raw) => Parse(raw).FirstOrDefault();

        private static List<string> Distinct(IEnumerable<string> names) =>
            names.Select(n => n.Trim())
                .Where(n => n.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }
}
