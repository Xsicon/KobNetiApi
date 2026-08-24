using System.Globalization;
using System.Text;
using System.Text.Json;
using KobNeti.Api.Auth;
using KobNeti.Api.Data;
using KobNeti.Api.DTOs;
using KobNeti.Api.Products;
using KobNeti.Api.Shared;
using System.Security.Claims;

namespace KobNeti.Api.Services;

public interface IOverviewService
{
    Task<Response<OverviewDTO>> GetAsync(string tenantId, string displayName, Guid? userId);
    Task<Response<CrossProductOverviewDTO>> GetCrossProductAsync(ClaimsPrincipal user);
}

public class OverviewService : IOverviewService
{
    private readonly ISupportStore _store;
    private readonly IProductRegistry _products;

    public OverviewService(ISupportStore store, IProductRegistry products)
    {
        _store = store;
        _products = products;
    }

    public async Task<Response<OverviewDTO>> GetAsync(string tenantId, string displayName, Guid? userId)
    {
        var dto = await BuildOverviewAsync(tenantId, displayName, userId);
        return Response<OverviewDTO>.SuccessResponse(dto, "Overview loaded");
    }

    public async Task<Response<CrossProductOverviewDTO>> GetCrossProductAsync(ClaimsPrincipal user)
    {
        var products = await _products.ListEnabledAsync();
        var list = new List<OverviewDTO>();
        foreach (var p in products.Where(p => AdminRoleClaims.CanAccessProduct(user, p.Slug)))
            list.Add(await BuildOverviewAsync(p.Slug, p.DisplayName, AdminRoleClaims.GetUserId(user)));

        return Response<CrossProductOverviewDTO>.SuccessResponse(new CrossProductOverviewDTO
        {
            Products = list,
            TotalOpenTickets = list.Sum(x => x.OpenTickets),
            TotalActiveChats = list.Sum(x => x.ActiveChats),
            TotalOpenIncidents = list.Sum(x => x.OpenIncidents),
            TotalPendingApprovals = list.Sum(x => x.PendingApprovals)
        }, "Cross-product overview loaded");
    }

    private async Task<OverviewDTO> BuildOverviewAsync(string tenantId, string displayName, Guid? userId)
    {
        var openTickets = await _store.CountOpenTicketsAsync(tenantId);
        var activeChats = await _store.CountActiveChatsAsync(tenantId);
        var (incidents, _) = await _store.ListIncidentsAsync(tenantId, null, 1, 500);
        var openIncidents = incidents.Count(i =>
            i.Status is IncidentStatus.Open or IncidentStatus.Investigating or IncidentStatus.Mitigated);
        var (tasks, _) = await _store.ListEngTasksAsync(tenantId, EngTaskStatus.InProgress, null, 1, 200);
        var approvals = await _store.ListApprovalsAsync(tenantId, ApprovalStatus.Pending);
        var notes = await _store.ListNotificationsAsync(tenantId, userId, unreadOnly: true);

        return new OverviewDTO
        {
            TenantId = tenantId,
            DisplayName = displayName,
            ActiveChats = activeChats,
            OpenTickets = openTickets,
            OpenIncidents = openIncidents,
            EngTasksInProgress = tasks.Count,
            PendingApprovals = approvals.Count,
            UnreadNotifications = notes.Count
        };
    }
}

public interface IReportService
{
    Task<Response<ReportRunDTO>> RunAsync(string tenantId, RunReportDTO request, string? createdByName);
    Task<Response<List<ReportRunDTO>>> ListAsync(string tenantId);
    Task<Response<string>> GetCsvAsync(string tenantId, Guid id);
}

public class ReportService : IReportService
{
    private readonly ISupportStore _store;

    public ReportService(ISupportStore store) => _store = store;

    public async Task<Response<ReportRunDTO>> RunAsync(string tenantId, RunReportDTO request, string? createdByName)
    {
        var type = (request.ReportType ?? "tickets").Trim().ToLowerInvariant();
        if (type is not ("tickets" or "time" or "payroll" or "audit"))
            return Response<ReportRunDTO>.Fail("reportType must be tickets, time, payroll, or audit");

        var from = request.From ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var to = request.To ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var (csv, rows) = type switch
        {
            "time" => await BuildTimeCsv(tenantId, from, to),
            "payroll" => await BuildPayrollCsv(tenantId),
            "audit" => await BuildAuditCsv(tenantId, from, to),
            _ => await BuildTicketsCsv(tenantId, from, to)
        };

        var run = new ReportRunEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReportType = type,
            Label = string.IsNullOrWhiteSpace(request.Label) ? $"{type} {from:yyyy-MM-dd}–{to:yyyy-MM-dd}" : request.Label.Trim(),
            ParamsJson = JsonSerializer.Serialize(new { from, to }),
            RowCount = rows,
            CsvContent = csv,
            CreatedByName = createdByName,
            CreatedAt = DateTime.UtcNow
        };
        await _store.InsertReportRunAsync(run);
        return Response<ReportRunDTO>.SuccessResponse(Map(run), "Report generated");
    }

    public async Task<Response<List<ReportRunDTO>>> ListAsync(string tenantId)
    {
        var items = await _store.ListReportRunsAsync(tenantId);
        return Response<List<ReportRunDTO>>.SuccessResponse(items.Select(Map).ToList(), "Reports loaded");
    }

    public async Task<Response<string>> GetCsvAsync(string tenantId, Guid id)
    {
        var run = await _store.GetReportRunAsync(tenantId, id);
        if (run is null)
            return Response<string>.Fail("Report not found");
        return Response<string>.SuccessResponse(run.CsvContent, "CSV loaded");
    }

    private async Task<(string Csv, int Rows)> BuildTicketsCsv(string tenantId, DateOnly from, DateOnly to)
    {
        var (items, _) = await _store.ListTicketsAsync(tenantId, null, 1, 2000);
        var filtered = items.Where(t =>
        {
            var d = DateOnly.FromDateTime(t.CreatedAt);
            return d >= from && d <= to;
        }).ToList();
        var sb = new StringBuilder();
        sb.AppendLine("id,number,subject,status,priority,created_at");
        foreach (var t in filtered)
            sb.AppendLine($"{t.Id},{t.TicketNumber},{Csv(t.Subject)},{t.Status},{t.Priority},{t.CreatedAt:o}");
        return (sb.ToString(), filtered.Count);
    }

    private async Task<(string Csv, int Rows)> BuildTimeCsv(string tenantId, DateOnly from, DateOnly to)
    {
        var items = await _store.ListTimeEntriesAsync(tenantId, null, null);
        var filtered = items.Where(e =>
        {
            var d = DateOnly.FromDateTime(e.ClockIn ?? e.CreatedAt);
            return d >= from && d <= to;
        }).ToList();
        var sb = new StringBuilder();
        sb.AppendLine("id,user_name,work_date,minutes,status");
        foreach (var e in filtered)
        {
            var d = DateOnly.FromDateTime(e.ClockIn ?? e.CreatedAt);
            sb.AppendLine($"{e.Id},{Csv(e.UserName)},{d:yyyy-MM-dd},{e.Minutes ?? 0},{e.Status}");
        }
        return (sb.ToString(), filtered.Count);
    }

    private async Task<(string Csv, int Rows)> BuildPayrollCsv(string tenantId)
    {
        var periods = await _store.ListPayPeriodsAsync(tenantId);
        var sb = new StringBuilder();
        sb.AppendLine("id,label,status,start,end,total_amount");
        foreach (var p in periods)
            sb.AppendLine($"{p.Id},{Csv(p.Label)},{p.Status},{p.StartsOn:yyyy-MM-dd},{p.EndsOn:yyyy-MM-dd},{p.TotalAmount.ToString(CultureInfo.InvariantCulture)}");
        return (sb.ToString(), periods.Count);
    }

    private async Task<(string Csv, int Rows)> BuildAuditCsv(string tenantId, DateOnly from, DateOnly to)
    {
        var items = await _store.ListAuditEventsAsync(tenantId, null, null, null, 2000);
        var filtered = items.Where(e =>
        {
            var d = DateOnly.FromDateTime(e.CreatedAt);
            return d >= from && d <= to;
        }).ToList();
        var sb = new StringBuilder();
        sb.AppendLine("id,action,entity_type,entity_id,actor,created_at");
        foreach (var e in filtered)
            sb.AppendLine($"{e.Id},{Csv(e.Action)},{Csv(e.EntityType)},{Csv(e.EntityId)},{Csv(e.ActorName)},{e.CreatedAt:o}");
        return (sb.ToString(), filtered.Count);
    }

    private static string Csv(string? value)
    {
        var v = value ?? "";
        if (v.Contains(',') || v.Contains('"') || v.Contains('\n'))
            return $"\"{v.Replace("\"", "\"\"")}\"";
        return v;
    }

    private static ReportRunDTO Map(ReportRunEntity r) => new()
    {
        Id = r.Id,
        ReportType = r.ReportType,
        Label = r.Label,
        RowCount = r.RowCount,
        CreatedAt = r.CreatedAt,
        CreatedByName = r.CreatedByName
    };
}

public interface IPlatformHelpService
{
    Task<Response<List<PlatformHelpArticleDTO>>> ListAsync(bool publishedOnly);
    Task<Response<PlatformHelpArticleDTO>> GetBySlugAsync(string slug);
    Task<Response<PlatformHelpArticleDTO>> UpsertAsync(SavePlatformHelpDTO request);
}

public class PlatformHelpService : IPlatformHelpService
{
    private readonly ISupportStore _store;

    public PlatformHelpService(ISupportStore store) => _store = store;

    public async Task<Response<List<PlatformHelpArticleDTO>>> ListAsync(bool publishedOnly)
    {
        var items = await _store.ListPlatformHelpAsync(publishedOnly);
        return Response<List<PlatformHelpArticleDTO>>.SuccessResponse(items.Select(Map).ToList(), "Help articles loaded");
    }

    public async Task<Response<PlatformHelpArticleDTO>> GetBySlugAsync(string slug)
    {
        var article = await _store.GetPlatformHelpBySlugAsync(slug);
        if (article is null)
            return Response<PlatformHelpArticleDTO>.Fail("Article not found");
        return Response<PlatformHelpArticleDTO>.SuccessResponse(Map(article), "Article loaded");
    }

    public async Task<Response<PlatformHelpArticleDTO>> UpsertAsync(SavePlatformHelpDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Slug) || string.IsNullOrWhiteSpace(request.Title))
            return Response<PlatformHelpArticleDTO>.Fail("Slug and title are required");

        var now = DateTime.UtcNow;
        var existing = await _store.GetPlatformHelpBySlugAsync(request.Slug.Trim());
        var entity = new PlatformHelpArticleEntity
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            Slug = request.Slug.Trim().ToLowerInvariant(),
            Title = request.Title.Trim(),
            Body = request.Body ?? "",
            Category = string.IsNullOrWhiteSpace(request.Category) ? "general" : request.Category.Trim(),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "published" : request.Status.Trim(),
            SortOrder = request.SortOrder,
            CreatedAt = existing?.CreatedAt ?? now,
            UpdatedAt = now
        };
        var saved = await _store.UpsertPlatformHelpAsync(entity);
        return Response<PlatformHelpArticleDTO>.SuccessResponse(Map(saved), "Article saved");
    }

    private static PlatformHelpArticleDTO Map(PlatformHelpArticleEntity a) => new()
    {
        Id = a.Id,
        Slug = a.Slug,
        Title = a.Title,
        Body = a.Body,
        Category = a.Category,
        Status = a.Status,
        SortOrder = a.SortOrder
    };
}

public interface IInternalChatService
{
    Task<Response<List<ImChannelDTO>>> ListChannelsAsync(string tenantId);
    Task<Response<ImChannelDTO>> CreateChannelAsync(string tenantId, CreateImChannelDTO request, Guid? userId);
    Task<Response<List<ImMessageDTO>>> ListMessagesAsync(string tenantId, Guid channelId);
    Task<Response<ImMessageDTO>> SendAsync(string tenantId, Guid channelId, SendImMessageDTO request, Guid? userId, string? senderName);
}

public class InternalChatService : IInternalChatService
{
    private readonly ISupportStore _store;
    private readonly IAppNotificationService _notifications;

    public InternalChatService(ISupportStore store, IAppNotificationService notifications)
    {
        _store = store;
        _notifications = notifications;
    }

    public async Task<Response<List<ImChannelDTO>>> ListChannelsAsync(string tenantId)
    {
        var items = await _store.ListImChannelsAsync(tenantId);
        return Response<List<ImChannelDTO>>.SuccessResponse(items.Select(MapChannel).ToList(), "Channels loaded");
    }

    public async Task<Response<ImChannelDTO>> CreateChannelAsync(string tenantId, CreateImChannelDTO request, Guid? userId)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Response<ImChannelDTO>.Fail("Name is required");
        var type = string.Equals(request.ChannelType, "dm", StringComparison.OrdinalIgnoreCase) ? "dm" : "channel";
        var channel = new ImChannelEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            ChannelType = type,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };
        await _store.InsertImChannelAsync(channel);
        return Response<ImChannelDTO>.SuccessResponse(MapChannel(channel), "Channel created");
    }

    public async Task<Response<List<ImMessageDTO>>> ListMessagesAsync(string tenantId, Guid channelId)
    {
        var channel = await _store.GetImChannelAsync(tenantId, channelId);
        if (channel is null)
            return Response<List<ImMessageDTO>>.Fail("Channel not found");
        var items = await _store.ListImMessagesAsync(tenantId, channelId);
        return Response<List<ImMessageDTO>>.SuccessResponse(items.Select(MapMessage).ToList(), "Messages loaded");
    }

    public async Task<Response<ImMessageDTO>> SendAsync(
        string tenantId, Guid channelId, SendImMessageDTO request, Guid? userId, string? senderName)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            return Response<ImMessageDTO>.Fail("Message body is required");
        var channel = await _store.GetImChannelAsync(tenantId, channelId);
        if (channel is null)
            return Response<ImMessageDTO>.Fail("Channel not found");

        var msg = new ImMessageEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ChannelId = channelId,
            SenderUserId = userId,
            SenderName = string.IsNullOrWhiteSpace(senderName) ? "Agent" : senderName.Trim(),
            Body = request.Body.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        await _store.InsertImMessageAsync(msg);

        await _notifications.NotifyAsync(
            tenantId, "im",
            $"New message in #{channel.Name}",
            Truncate(msg.Body, 120),
            sourceType: "im_channel",
            sourceId: channel.Id.ToString(),
            linkUrl: "/admin/support?tab=internal");

        return Response<ImMessageDTO>.SuccessResponse(MapMessage(msg), "Message sent");
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";

    private static ImChannelDTO MapChannel(ImChannelEntity c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        ChannelType = c.ChannelType,
        CreatedAt = c.CreatedAt
    };

    private static ImMessageDTO MapMessage(ImMessageEntity m) => new()
    {
        Id = m.Id,
        ChannelId = m.ChannelId,
        SenderUserId = m.SenderUserId,
        SenderName = m.SenderName,
        Body = m.Body,
        CreatedAt = m.CreatedAt
    };
}

public interface IAssetService
{
    Task<Response<List<AssetDTO>>> ListAsync(string tenantId);
    Task<Response<AssetDTO>> CreateAsync(string tenantId, SaveAssetDTO request);
    Task<Response<AssetDTO>> AssignAsync(string tenantId, Guid id, AssignAssetDTO request);
    Task<Response<AssetDTO>> RetireAsync(string tenantId, Guid id);
    Task<Response<int>> SendRenewalRemindersAsync(string tenantId, int withinDays = 30);
}

public class AssetService : IAssetService
{
    private readonly ISupportStore _store;
    private readonly IAppNotificationService _notifications;

    public AssetService(ISupportStore store, IAppNotificationService notifications)
    {
        _store = store;
        _notifications = notifications;
    }

    public async Task<Response<List<AssetDTO>>> ListAsync(string tenantId)
    {
        var items = await _store.ListAssetsAsync(tenantId);
        return Response<List<AssetDTO>>.SuccessResponse(items.Select(Map).ToList(), "Assets loaded");
    }

    public async Task<Response<AssetDTO>> CreateAsync(string tenantId, SaveAssetDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Response<AssetDTO>.Fail("Name is required");
        var type = (request.AssetType ?? "hardware").Trim().ToLowerInvariant();
        if (type is not ("hardware" or "license" or "other"))
            type = "hardware";

        var now = DateTime.UtcNow;
        var asset = new AssetEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            AssetType = type,
            SerialOrKey = request.SerialOrKey,
            Status = "available",
            RenewalDate = request.RenewalDate,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _store.InsertAssetAsync(asset);
        return Response<AssetDTO>.SuccessResponse(Map(asset), "Asset created");
    }

    public async Task<Response<AssetDTO>> AssignAsync(string tenantId, Guid id, AssignAssetDTO request)
    {
        var asset = await _store.GetAssetAsync(tenantId, id);
        if (asset is null)
            return Response<AssetDTO>.Fail("Asset not found");
        if (asset.Status == "retired")
            return Response<AssetDTO>.Fail("Cannot assign a retired asset");

        if (request.UserId is null && string.IsNullOrWhiteSpace(request.UserName))
        {
            asset.AssignedUserId = null;
            asset.AssignedUserName = null;
            asset.Status = "available";
        }
        else
        {
            asset.AssignedUserId = request.UserId;
            asset.AssignedUserName = string.IsNullOrWhiteSpace(request.UserName) ? "User" : request.UserName.Trim();
            asset.Status = "assigned";
        }
        asset.UpdatedAt = DateTime.UtcNow;
        await _store.UpdateAssetAsync(asset);

        if (asset.Status == "assigned")
        {
            await _notifications.NotifyAsync(
                tenantId, "assign",
                $"Asset assigned: {asset.Name}",
                $"Assigned to {asset.AssignedUserName}",
                userId: asset.AssignedUserId,
                userName: asset.AssignedUserName,
                sourceType: "asset",
                sourceId: asset.Id.ToString());
        }

        return Response<AssetDTO>.SuccessResponse(Map(asset), "Asset updated");
    }

    public async Task<Response<AssetDTO>> RetireAsync(string tenantId, Guid id)
    {
        var asset = await _store.GetAssetAsync(tenantId, id);
        if (asset is null)
            return Response<AssetDTO>.Fail("Asset not found");
        asset.Status = "retired";
        asset.AssignedUserId = null;
        asset.AssignedUserName = null;
        asset.UpdatedAt = DateTime.UtcNow;
        await _store.UpdateAssetAsync(asset);
        return Response<AssetDTO>.SuccessResponse(Map(asset), "Asset retired");
    }

    public async Task<Response<int>> SendRenewalRemindersAsync(string tenantId, int withinDays = 30)
    {
        var before = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(Math.Clamp(withinDays, 1, 365)));
        var soon = await _store.ListAssetsRenewingSoonAsync(tenantId, before);
        var count = 0;
        foreach (var a in soon)
        {
            await _notifications.NotifyAsync(
                tenantId, "reminder",
                $"Asset renewal: {a.Name}",
                $"Renews on {a.RenewalDate:yyyy-MM-dd}",
                userId: a.AssignedUserId,
                userName: a.AssignedUserName,
                sourceType: "asset",
                sourceId: a.Id.ToString());
            count++;
        }
        return Response<int>.SuccessResponse(count, "Renewal reminders sent");
    }

    private static AssetDTO Map(AssetEntity a) => new()
    {
        Id = a.Id,
        Name = a.Name,
        AssetType = a.AssetType,
        SerialOrKey = a.SerialOrKey,
        Status = a.Status,
        AssignedUserId = a.AssignedUserId,
        AssignedUserName = a.AssignedUserName,
        RenewalDate = a.RenewalDate,
        Notes = a.Notes
    };
}
