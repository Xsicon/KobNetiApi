using KobNeti.Api.Data;
using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;
using KobNeti.Api.Staff;

namespace KobNeti.Api.Services;

public interface IMilestoneService
{
    Task<Response<List<MilestoneDTO>>> ListAsync(string tenantId);
    Task<Response<MilestoneDTO>> GetAsync(string tenantId, Guid id);
    Task<Response<MilestoneDTO>> CreateAsync(string tenantId, CreateMilestoneDTO request);
    Task<Response<MilestoneDTO>> UpdateAsync(string tenantId, Guid id, UpdateMilestoneDTO request);
    Task<Response<List<CalendarEventDTO>>> ListCalendarAsync(string tenantId);
}

public class MilestoneService : IMilestoneService
{
    private readonly ISupportStore _store;
    private readonly IStaffDirectory _staff;

    public MilestoneService(ISupportStore store, IStaffDirectory staff)
    {
        _store = store;
        _staff = staff;
    }

    public async Task<Response<List<MilestoneDTO>>> ListAsync(string tenantId)
    {
        await EngineeringSampleData.EnsureSeededAsync(_store, tenantId, _staff);
        var milestones = await _store.ListMilestonesAsync(tenantId);
        var (tasks, _) = await _store.ListEngTasksAsync(tenantId, null, null, 1, 500);
        var counts = tasks.Where(t => t.MilestoneId.HasValue)
            .GroupBy(t => t.MilestoneId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var list = milestones.Select(m => Map(m, counts.GetValueOrDefault(m.Id))).ToList();
        return Response<List<MilestoneDTO>>.SuccessResponse(list, "Milestones loaded");
    }

    public async Task<Response<MilestoneDTO>> GetAsync(string tenantId, Guid id)
    {
        var m = await _store.GetMilestoneAsync(tenantId, id);
        if (m is null)
            return Response<MilestoneDTO>.Fail("Milestone not found");
        var (tasks, _) = await _store.ListEngTasksAsync(tenantId, null, id, 1, 500);
        return Response<MilestoneDTO>.SuccessResponse(Map(m, tasks.Count), "Milestone loaded");
    }

    public async Task<Response<MilestoneDTO>> CreateAsync(string tenantId, CreateMilestoneDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Response<MilestoneDTO>.Fail("Title is required");

        var status = MilestoneStatus.Normalize(request.Status);
        if (status is null)
            return Response<MilestoneDTO>.Fail("Invalid status");

        var now = DateTime.UtcNow;
        var milestone = new EngMilestoneEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Status = status,
            TargetDate = request.TargetDate,
            StartDate = request.StartDate,
            SortOrder = request.SortOrder,
            CreatedAt = now,
            UpdatedAt = now
        };
        var saved = await _store.InsertMilestoneAsync(milestone);
        await SyncCalendarAsync(tenantId, saved);
        return Response<MilestoneDTO>.SuccessResponse(Map(saved, 0), "Milestone created");
    }

    public async Task<Response<MilestoneDTO>> UpdateAsync(string tenantId, Guid id, UpdateMilestoneDTO request)
    {
        var milestone = await _store.GetMilestoneAsync(tenantId, id);
        if (milestone is null)
            return Response<MilestoneDTO>.Fail("Milestone not found");

        if (!string.IsNullOrWhiteSpace(request.Title))
            milestone.Title = request.Title.Trim();
        if (request.Description != null)
            milestone.Description = request.Description;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = MilestoneStatus.Normalize(request.Status);
            if (status is null)
                return Response<MilestoneDTO>.Fail("Invalid status");
            milestone.Status = status;
        }

        if (request.ClearTargetDate)
            milestone.TargetDate = null;
        else if (request.TargetDate.HasValue)
            milestone.TargetDate = request.TargetDate;

        if (request.ClearStartDate)
            milestone.StartDate = null;
        else if (request.StartDate.HasValue)
            milestone.StartDate = request.StartDate;

        if (request.SortOrder.HasValue)
            milestone.SortOrder = request.SortOrder.Value;

        milestone.UpdatedAt = DateTime.UtcNow;
        await _store.UpdateMilestoneAsync(milestone);
        await SyncCalendarAsync(tenantId, milestone);

        var (tasks, _) = await _store.ListEngTasksAsync(tenantId, null, id, 1, 500);
        return Response<MilestoneDTO>.SuccessResponse(Map(milestone, tasks.Count), "Milestone updated");
    }

    public async Task<Response<List<CalendarEventDTO>>> ListCalendarAsync(string tenantId)
    {
        var events = await _store.ListCalendarEventsAsync(tenantId);
        var list = events.Select(e => new CalendarEventDTO
        {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            EventType = e.EventType,
            StartsAt = e.StartsAt,
            EndsAt = e.EndsAt,
            Location = e.Location,
            SourceEntityType = e.SourceEntityType,
            SourceEntityId = e.SourceEntityId
        }).ToList();
        return Response<List<CalendarEventDTO>>.SuccessResponse(list, "Calendar events loaded");
    }

    private async Task SyncCalendarAsync(string tenantId, EngMilestoneEntity milestone)
    {
        if (!milestone.TargetDate.HasValue)
        {
            if (milestone.CalendarEventId.HasValue)
            {
                await _store.DeleteCalendarEventAsync(tenantId, milestone.CalendarEventId.Value);
                milestone.CalendarEventId = null;
                await _store.UpdateMilestoneAsync(milestone);
            }
            return;
        }

        var starts = milestone.TargetDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var evt = new CalendarEventEntity
        {
            Id = milestone.CalendarEventId ?? Guid.NewGuid(),
            TenantId = tenantId,
            Title = $"Milestone: {milestone.Title}",
            Description = milestone.Description,
            EventType = "milestone",
            StartsAt = starts,
            EndsAt = starts.AddDays(1),
            SourceEntityType = "eng_milestone",
            SourceEntityId = milestone.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _store.UpsertCalendarEventAsync(evt);
        if (milestone.CalendarEventId != evt.Id)
        {
            milestone.CalendarEventId = evt.Id;
            await _store.UpdateMilestoneAsync(milestone);
        }
    }

    private static MilestoneDTO Map(EngMilestoneEntity m, int taskCount) => new()
    {
        Id = m.Id,
        Title = m.Title,
        Description = m.Description,
        Status = m.Status,
        TargetDate = m.TargetDate,
        StartDate = m.StartDate,
        SortOrder = m.SortOrder,
        CalendarEventId = m.CalendarEventId,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
        TaskCount = taskCount
    };
}
