using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KobNeti.Api.Data;
using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;
using KobNeti.Api.Staff;

namespace KobNeti.Api.Services;

public interface ITimeTrackingService
{
    Task<Response<List<TimeEntryDTO>>> ListAsync(string tenantId, Guid? userId, string? status);
    Task<Response<TimeEntryDTO>> ClockInAsync(string tenantId, ClockInDTO request, Guid? userId, string userName);
    Task<Response<TimeEntryDTO>> ClockOutAsync(string tenantId, Guid? userId, string userName);
    Task<Response<TimeEntryDTO>> AddManualAsync(string tenantId, ManualTimeDTO request, Guid? userId, string userName);
    Task<Response<ApprovalRequestDTO>> RequestEditAsync(string tenantId, RequestTimeEditDTO request, Guid? userId, string userName);
}

public interface IApprovalService
{
    Task<Response<List<ApprovalRequestDTO>>> ListAsync(string tenantId, string? status);
    Task<Response<ApprovalRequestDTO>> DecideAsync(string tenantId, Guid id, DecideApprovalDTO request, Guid? approverId, string approverName);
}

public interface IPayrollService
{
    Task<Response<List<PayRateDTO>>> ListRatesAsync(string tenantId);
    Task<Response<PayRateDTO>> SaveRateAsync(string tenantId, SavePayRateDTO request);
    Task<Response<List<PayPeriodDTO>>> ListPeriodsAsync(string tenantId);
    Task<Response<PayPeriodDTO>> CreatePeriodAsync(string tenantId, CreatePayPeriodDTO request);
    Task<Response<PayPeriodDTO>> CalculateAsync(string tenantId, Guid periodId);
    Task<Response<PayPeriodDTO>> SubmitForApprovalAsync(string tenantId, Guid periodId, Guid? userId, string userName);
    Task<Response<PayPeriodDTO>> FinalizeAsync(string tenantId, Guid periodId);
    Task<Response<string>> ExportCsvAsync(string tenantId, Guid periodId);
    Task<Response<byte[]>> ExportPdfAsync(string tenantId, Guid periodId, string companyName);
}

public class TimeTrackingService : ITimeTrackingService
{
    private readonly ISupportStore _store;

    public TimeTrackingService(ISupportStore store) => _store = store;

    public async Task<Response<List<TimeEntryDTO>>> ListAsync(string tenantId, Guid? userId, string? status)
    {
        var items = await _store.ListTimeEntriesAsync(tenantId, userId, status);
        return Response<List<TimeEntryDTO>>.SuccessResponse(items.Select(Map).ToList(), "Time entries loaded");
    }

    public async Task<Response<TimeEntryDTO>> ClockInAsync(
        string tenantId, ClockInDTO request, Guid? userId, string userName)
    {
        var open = await _store.GetOpenClockAsync(tenantId, userId);
        if (open is not null)
            return Response<TimeEntryDTO>.Fail("Already clocked in");

        if (request.TicketId.HasValue && await _store.GetTicketAsync(tenantId, request.TicketId.Value) is null)
            return Response<TimeEntryDTO>.Fail("Ticket not found");
        if (request.EngTaskId.HasValue && await _store.GetEngTaskAsync(tenantId, request.EngTaskId.Value) is null)
            return Response<TimeEntryDTO>.Fail("Task not found");

        var now = DateTime.UtcNow;
        var entry = new TimeEntryEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            UserName = userName,
            EntryType = TimeEntryType.Clock,
            ClockIn = now,
            TicketId = request.TicketId,
            EngTaskId = request.EngTaskId,
            Notes = request.Notes?.Trim(),
            Status = TimeEntryStatus.Open,
            CreatedAt = now
        };
        await _store.InsertTimeEntryAsync(entry);
        return Response<TimeEntryDTO>.SuccessResponse(Map(entry), "Clocked in");
    }

    public async Task<Response<TimeEntryDTO>> ClockOutAsync(string tenantId, Guid? userId, string userName)
    {
        var open = await _store.GetOpenClockAsync(tenantId, userId);
        if (open is null)
            return Response<TimeEntryDTO>.Fail("No open clock session");

        var now = DateTime.UtcNow;
        open.ClockOut = now;
        open.Minutes = Math.Max(1, (int)Math.Round((now - open.ClockIn!.Value).TotalMinutes));
        open.Status = TimeEntryStatus.Approved;
        if (string.IsNullOrWhiteSpace(open.UserName))
            open.UserName = userName;
        await _store.UpdateTimeEntryAsync(open);
        return Response<TimeEntryDTO>.SuccessResponse(Map(open), "Clocked out");
    }

    public async Task<Response<TimeEntryDTO>> AddManualAsync(
        string tenantId, ManualTimeDTO request, Guid? userId, string userName)
    {
        if (request.Minutes <= 0)
            return Response<TimeEntryDTO>.Fail("Minutes must be positive");
        if (request.TicketId.HasValue && await _store.GetTicketAsync(tenantId, request.TicketId.Value) is null)
            return Response<TimeEntryDTO>.Fail("Ticket not found");
        if (request.EngTaskId.HasValue && await _store.GetEngTaskAsync(tenantId, request.EngTaskId.Value) is null)
            return Response<TimeEntryDTO>.Fail("Task not found");

        var workedAt = request.WorkedAt?.ToUniversalTime() ?? DateTime.UtcNow;
        var entry = new TimeEntryEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            UserName = userName,
            EntryType = TimeEntryType.Manual,
            ClockIn = workedAt,
            ClockOut = workedAt.AddMinutes(request.Minutes),
            Minutes = request.Minutes,
            TicketId = request.TicketId,
            EngTaskId = request.EngTaskId,
            Notes = request.Notes?.Trim(),
            Status = TimeEntryStatus.Approved,
            CreatedAt = DateTime.UtcNow
        };
        await _store.InsertTimeEntryAsync(entry);
        return Response<TimeEntryDTO>.SuccessResponse(Map(entry), "Manual time added");
    }

    public async Task<Response<ApprovalRequestDTO>> RequestEditAsync(
        string tenantId, RequestTimeEditDTO request, Guid? userId, string userName)
    {
        var original = await _store.GetTimeEntryAsync(tenantId, request.EntryId);
        if (original is null)
            return Response<ApprovalRequestDTO>.Fail("Time entry not found");
        if (original.Status is TimeEntryStatus.Superseded or TimeEntryStatus.Open or TimeEntryStatus.Pending)
            return Response<ApprovalRequestDTO>.Fail("Entry cannot be edited in its current status");

        var minutes = request.Minutes ?? original.Minutes;
        if (minutes is null or <= 0)
            return Response<ApprovalRequestDTO>.Fail("Minutes must be positive");

        var now = DateTime.UtcNow;
        var pending = new TimeEntryEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = original.UserId,
            UserName = original.UserName,
            EntryType = original.EntryType,
            ClockIn = request.ClockIn ?? original.ClockIn,
            ClockOut = request.ClockOut ?? original.ClockOut,
            Minutes = minutes,
            TicketId = original.TicketId,
            EngTaskId = original.EngTaskId,
            Notes = request.Notes ?? original.Notes,
            Status = TimeEntryStatus.Pending,
            SupersedesId = original.Id,
            CreatedAt = now
        };
        await _store.InsertTimeEntryAsync(pending);

        var approval = new ApprovalRequestEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RequestType = ApprovalRequestType.TimeEdit,
            Status = ApprovalStatus.Pending,
            PayloadJson = JsonSerializer.Serialize(new
            {
                originalEntryId = original.Id,
                pendingEntryId = pending.Id,
                reason = request.Reason,
                minutes
            }),
            RequesterUserId = userId,
            RequesterName = userName,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _store.InsertApprovalAsync(approval);
        pending.ApprovalId = approval.Id;
        await _store.UpdateTimeEntryAsync(pending);

        return Response<ApprovalRequestDTO>.SuccessResponse(MapApproval(approval), "Time edit submitted for approval");
    }

    internal static TimeEntryDTO Map(TimeEntryEntity e) => new()
    {
        Id = e.Id,
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

    internal static ApprovalRequestDTO MapApproval(ApprovalRequestEntity a) => new()
    {
        Id = a.Id,
        RequestType = a.RequestType,
        Status = a.Status,
        PayloadJson = a.PayloadJson,
        RequesterUserId = a.RequesterUserId,
        RequesterName = a.RequesterName,
        ApproverUserId = a.ApproverUserId,
        ApproverName = a.ApproverName,
        DecidedAt = a.DecidedAt,
        CreatedAt = a.CreatedAt
    };
}

public class ApprovalService : IApprovalService
{
    private readonly ISupportStore _store;
    private readonly IAuditService _audit;
    private readonly IAppNotificationService _notify;

    public ApprovalService(ISupportStore store, IAuditService audit, IAppNotificationService notify)
    {
        _store = store;
        _audit = audit;
        _notify = notify;
    }

    public async Task<Response<List<ApprovalRequestDTO>>> ListAsync(string tenantId, string? status)
    {
        var items = await _store.ListApprovalsAsync(tenantId, status);
        return Response<List<ApprovalRequestDTO>>.SuccessResponse(
            items.Select(TimeTrackingService.MapApproval).ToList(), "Approvals loaded");
    }

    public async Task<Response<ApprovalRequestDTO>> DecideAsync(
        string tenantId, Guid id, DecideApprovalDTO request, Guid? approverId, string approverName)
    {
        var approval = await _store.GetApprovalAsync(tenantId, id);
        if (approval is null)
            return Response<ApprovalRequestDTO>.Fail("Approval not found");
        if (approval.Status != ApprovalStatus.Pending)
            return Response<ApprovalRequestDTO>.Fail("Approval already decided");

        var now = DateTime.UtcNow;
        if (!request.Approve && !string.IsNullOrWhiteSpace(request.Comment))
            approval.PayloadJson = MergePayloadComment(approval.PayloadJson, request.Comment.Trim());
        approval.Status = request.Approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        approval.ApproverUserId = approverId;
        approval.ApproverName = approverName;
        approval.DecidedAt = now;
        approval.UpdatedAt = now;
        await _store.UpdateApprovalAsync(approval);

        if (approval.RequestType == ApprovalRequestType.TimeEdit)
            await ApplyTimeEditDecisionAsync(tenantId, approval, request.Approve);
        else if (approval.RequestType == ApprovalRequestType.Payroll)
            await ApplyPayrollDecisionAsync(tenantId, approval, request.Approve);

        await _audit.WriteAsync(tenantId, AuditActions.ApprovalDecide, "approval", approval.Id.ToString(),
            approverId, approverName, new { status = "pending" }, new { status = approval.Status });
        await _notify.NotifyAsync(tenantId, "approval",
            $"Approval {approval.Status}: {approval.RequestType}",
            null, approval.RequesterUserId, approval.RequesterName, "approval", approval.Id.ToString());

        return Response<ApprovalRequestDTO>.SuccessResponse(
            TimeTrackingService.MapApproval(approval),
            request.Approve ? "Approved" : "Rejected");
    }

    private async Task ApplyTimeEditDecisionAsync(string tenantId, ApprovalRequestEntity approval, bool approve)
    {
        using var doc = JsonDocument.Parse(approval.PayloadJson);
        if (!doc.RootElement.TryGetProperty("pendingEntryId", out var pendingEl) ||
            !doc.RootElement.TryGetProperty("originalEntryId", out var originalEl))
            return;
        if (!Guid.TryParse(pendingEl.GetString(), out var pendingId) ||
            !Guid.TryParse(originalEl.GetString(), out var originalId))
            return;

        var pending = await _store.GetTimeEntryAsync(tenantId, pendingId);
        var original = await _store.GetTimeEntryAsync(tenantId, originalId);
        if (pending is null)
            return;

        if (approve)
        {
            pending.Status = TimeEntryStatus.Approved;
            await _store.UpdateTimeEntryAsync(pending);
            if (original is not null)
            {
                original.Status = TimeEntryStatus.Superseded;
                await _store.UpdateTimeEntryAsync(original);
            }
        }
        else
        {
            pending.Status = TimeEntryStatus.Rejected;
            await _store.UpdateTimeEntryAsync(pending);
        }
    }

    private async Task ApplyPayrollDecisionAsync(string tenantId, ApprovalRequestEntity approval, bool approve)
    {
        using var doc = JsonDocument.Parse(approval.PayloadJson);
        if (!doc.RootElement.TryGetProperty("payPeriodId", out var periodEl))
            return;
        if (!Guid.TryParse(periodEl.GetString(), out var periodId))
            return;
        var period = await _store.GetPayPeriodAsync(tenantId, periodId);
        if (period is null)
            return;
        period.Status = approve ? PayPeriodStatus.Approved : PayPeriodStatus.Calculated;
        period.UpdatedAt = DateTime.UtcNow;
        await _store.UpdatePayPeriodAsync(period);
    }

    private static string MergePayloadComment(string payloadJson, string comment)
    {
        try
        {
            var node = JsonNode.Parse(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson) as JsonObject
                       ?? new JsonObject();
            node["comment"] = comment;
            return node.ToJsonString();
        }
        catch
        {
            return payloadJson;
        }
    }
}

public class PayrollService : IPayrollService
{
    private readonly ISupportStore _store;
    private readonly IAuditService _audit;
    private readonly IStaffDirectory _staff;

    public PayrollService(ISupportStore store, IAuditService audit, IStaffDirectory staff)
    {
        _store = store;
        _audit = audit;
        _staff = staff;
    }

    public async Task<Response<List<PayRateDTO>>> ListRatesAsync(string tenantId)
    {
        await PeopleOpsSampleData.EnsureSeededAsync(_store, this, tenantId, _staff);
        var rates = await _store.ListPayRatesAsync(tenantId);
        return Response<List<PayRateDTO>>.SuccessResponse(rates.Select(MapRate).ToList(), "Rates loaded");
    }

    public async Task<Response<PayRateDTO>> SaveRateAsync(string tenantId, SavePayRateDTO request)
    {
        if (request.HourlyRate <= 0)
            return Response<PayRateDTO>.Fail("Hourly rate must be positive");
        if (request.StaffId is null && request.UserId is null && string.IsNullOrWhiteSpace(request.Role))
            return Response<PayRateDTO>.Fail("Provide staffId, userId, or role");

        var rate = new PayRateEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StaffId = request.StaffId,
            UserId = request.UserId,
            Role = string.IsNullOrWhiteSpace(request.Role) ? null : request.Role.Trim().ToLowerInvariant(),
            HourlyRate = request.HourlyRate,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.Trim().ToUpperInvariant(),
            EffectiveFrom = request.EffectiveFrom ?? DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTime.UtcNow
        };
        await _store.InsertPayRateAsync(rate);
        await _audit.WriteAsync(tenantId, AuditActions.PayrollRate, "pay_rate", rate.Id.ToString(),
            null, null, null, new { rate.HourlyRate, rate.Role, rate.UserId });
        return Response<PayRateDTO>.SuccessResponse(MapRate(rate), "Rate saved");
    }

    public async Task<Response<List<PayPeriodDTO>>> ListPeriodsAsync(string tenantId)
    {
        await PeopleOpsSampleData.EnsureSeededAsync(_store, this, tenantId, _staff);
        var periods = await _store.ListPayPeriodsAsync(tenantId);
        return Response<List<PayPeriodDTO>>.SuccessResponse(periods.Select(MapPeriod).ToList(), "Pay periods loaded");
    }

    public async Task<Response<PayPeriodDTO>> CreatePeriodAsync(string tenantId, CreatePayPeriodDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
            return Response<PayPeriodDTO>.Fail("Label is required");
        if (request.EndsOn < request.StartsOn)
            return Response<PayPeriodDTO>.Fail("endsOn must be on/after startsOn");

        var now = DateTime.UtcNow;
        var period = new PayPeriodEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Label = request.Label.Trim(),
            StartsOn = request.StartsOn,
            EndsOn = request.EndsOn,
            Status = PayPeriodStatus.Open,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _store.InsertPayPeriodAsync(period);
        return Response<PayPeriodDTO>.SuccessResponse(MapPeriod(period), "Pay period created");
    }

    public async Task<Response<PayPeriodDTO>> CalculateAsync(string tenantId, Guid periodId)
    {
        var period = await _store.GetPayPeriodAsync(tenantId, periodId);
        if (period is null)
            return Response<PayPeriodDTO>.Fail("Pay period not found");
        if (period.Status is PayPeriodStatus.PendingApproval or PayPeriodStatus.Approved or PayPeriodStatus.Exported)
            return Response<PayPeriodDTO>.Fail("Period is locked");

        var entries = await _store.ListTimeEntriesAsync(tenantId, null, TimeEntryStatus.Approved);
        var start = period.StartsOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = period.EndsOn.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc);
        var inRange = entries.Where(e =>
        {
            var at = e.ClockIn ?? e.CreatedAt;
            return at >= start && at <= end && e.Minutes.GetValueOrDefault() > 0;
        }).ToList();

        var rates = await _store.ListPayRatesAsync(tenantId);
        var lines = inRange
            .GroupBy(e => new { e.UserId, Name = e.UserName })
            .Select(g =>
            {
                var minutes = g.Sum(x => x.Minutes ?? 0);
                var rate = ResolveRate(rates, g.Key.UserId);
                var amount = Math.Round((minutes / 60m) * rate.HourlyRate, 2);
                return new PayPeriodLineDTO
                {
                    UserId = g.Key.UserId,
                    UserName = string.IsNullOrWhiteSpace(g.Key.Name) ? "Unknown" : g.Key.Name,
                    Minutes = minutes,
                    HourlyRate = rate.HourlyRate,
                    Amount = amount
                };
            })
            .OrderBy(l => l.UserName)
            .ToList();

        period.LinesJson = JsonSerializer.Serialize(lines);
        period.TotalMinutes = lines.Sum(l => l.Minutes);
        period.TotalAmount = lines.Sum(l => l.Amount);
        period.Currency = rates.FirstOrDefault()?.Currency ?? "USD";
        period.Status = PayPeriodStatus.Calculated;
        period.UpdatedAt = DateTime.UtcNow;
        await _store.UpdatePayPeriodAsync(period);
        await _audit.WriteAsync(tenantId, AuditActions.PayrollCalculate, "pay_period", period.Id.ToString(),
            null, null, null, new { period.TotalMinutes, period.TotalAmount });
        return Response<PayPeriodDTO>.SuccessResponse(MapPeriod(period), "Payroll calculated from approved time");
    }

    public async Task<Response<PayPeriodDTO>> SubmitForApprovalAsync(
        string tenantId, Guid periodId, Guid? userId, string userName)
    {
        var period = await _store.GetPayPeriodAsync(tenantId, periodId);
        if (period is null)
            return Response<PayPeriodDTO>.Fail("Pay period not found");
        if (period.Status != PayPeriodStatus.Calculated)
            return Response<PayPeriodDTO>.Fail("Calculate payroll before submitting");

        var now = DateTime.UtcNow;
        var approval = new ApprovalRequestEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RequestType = ApprovalRequestType.Payroll,
            Status = ApprovalStatus.Pending,
            PayloadJson = JsonSerializer.Serialize(new { payPeriodId = period.Id, totalAmount = period.TotalAmount }),
            RequesterUserId = userId,
            RequesterName = userName,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _store.InsertApprovalAsync(approval);
        period.ApprovalId = approval.Id;
        period.Status = PayPeriodStatus.PendingApproval;
        period.UpdatedAt = now;
        await _store.UpdatePayPeriodAsync(period);
        await _audit.WriteAsync(tenantId, AuditActions.PayrollSubmit, "pay_period", period.Id.ToString(),
            userId, userName, null, new { status = period.Status, approvalId = approval.Id });
        return Response<PayPeriodDTO>.SuccessResponse(MapPeriod(period), "Payroll submitted for approval");
    }

    public async Task<Response<PayPeriodDTO>> FinalizeAsync(string tenantId, Guid periodId)
    {
        var period = await _store.GetPayPeriodAsync(tenantId, periodId);
        if (period is null)
            return Response<PayPeriodDTO>.Fail("Pay period not found");
        if (period.Status == PayPeriodStatus.Exported)
            return Response<PayPeriodDTO>.SuccessResponse(MapPeriod(period), "Payroll already finalized");
        if (period.Status != PayPeriodStatus.Approved)
            return Response<PayPeriodDTO>.Fail("Approve payroll before finalizing");

        period.Status = PayPeriodStatus.Exported;
        period.UpdatedAt = DateTime.UtcNow;
        await _store.UpdatePayPeriodAsync(period);
        await _audit.WriteAsync(tenantId, AuditActions.PayrollExport, "pay_period", period.Id.ToString(),
            null, null, null, new { status = period.Status, finalized = true });
        return Response<PayPeriodDTO>.SuccessResponse(MapPeriod(period), "Payroll finalized");
    }

    public async Task<Response<string>> ExportCsvAsync(string tenantId, Guid periodId)
    {
        var period = await _store.GetPayPeriodAsync(tenantId, periodId);
        if (period is null)
            return Response<string>.Fail("Pay period not found");
        if (period.Status is not (PayPeriodStatus.Approved or PayPeriodStatus.Exported))
            return Response<string>.Fail("Export requires an approved payroll run");

        var lines = DeserializeLines(period.LinesJson);
        var sb = new StringBuilder();
        sb.AppendLine("user_name,user_id,minutes,hourly_rate,amount,currency,period");
        foreach (var line in lines)
        {
            sb.AppendLine(string.Join(',',
                Csv(line.UserName),
                line.UserId?.ToString() ?? "",
                line.Minutes,
                line.HourlyRate,
                line.Amount,
                period.Currency,
                Csv(period.Label)));
        }

        await _audit.WriteAsync(tenantId, AuditActions.PayrollExport, "pay_period", period.Id.ToString(),
            null, null, null, new { status = period.Status, exported = true });
        return Response<string>.SuccessResponse(sb.ToString(), "CSV export");
    }

    public async Task<Response<byte[]>> ExportPdfAsync(string tenantId, Guid periodId, string companyName)
    {
        var period = await _store.GetPayPeriodAsync(tenantId, periodId);
        if (period is null)
            return Response<byte[]>.Fail("Pay period not found");
        if (period.Status is not (PayPeriodStatus.Approved or PayPeriodStatus.Exported))
            return Response<byte[]>.Fail("Export requires an approved payroll run");

        var lines = DeserializeLines(period.LinesJson);
        var roster = await _staff.ListAsync();
        var statement = new PayrollStatementModel
        {
            CompanyName = string.IsNullOrWhiteSpace(companyName) ? tenantId : companyName.Trim(),
            PeriodLabel = string.IsNullOrWhiteSpace(period.Label)
                ? $"{period.StartsOn:MMM d} – {period.EndsOn:MMM d, yyyy}"
                : period.Label,
            StartsOn = period.StartsOn,
            EndsOn = period.EndsOn,
            GeneratedAt = DateTime.UtcNow,
            Status = period.Status,
            Currency = string.IsNullOrWhiteSpace(period.Currency) ? "USD" : period.Currency,
            PeriodId = period.Id,
            Lines = lines.Select(line =>
            {
                var person = roster.FirstOrDefault(s =>
                    (line.UserId.HasValue && (s.Id == line.UserId || s.UserId == line.UserId))
                    || string.Equals(s.DisplayName, line.UserName, StringComparison.OrdinalIgnoreCase));
                return new PayrollStatementLine
                {
                    Name = string.IsNullOrWhiteSpace(line.UserName) ? "Unknown" : line.UserName,
                    Role = TitleRole(person is null
                        ? "support"
                        : StaffRoles.Primary(person.Roles.Count > 0 ? person.Roles : [person.Role])),
                    Email = person?.Email ?? "",
                    Hours = Math.Round(line.Minutes / 60m, 1),
                    Rate = line.HourlyRate,
                    Amount = line.Amount
                };
            }).ToList()
        };

        var pdf = PayrollStatementPdf.Build(statement);
        await _audit.WriteAsync(tenantId, AuditActions.PayrollExport, "pay_period", period.Id.ToString(),
            null, null, null, new { status = period.Status, format = "pdf" });
        return Response<byte[]>.SuccessResponse(pdf, "PDF export");
    }

    private static string TitleRole(string role) =>
        string.IsNullOrWhiteSpace(role)
            ? "Support"
            : char.ToUpperInvariant(role[0]) + role[1..].ToLowerInvariant();

    private static (decimal HourlyRate, string Currency) ResolveRate(List<PayRateEntity> rates, Guid? userId)
    {
        var byPerson = rates.FirstOrDefault(r => userId.HasValue && (r.UserId == userId || r.StaffId == userId));
        if (byPerson is not null)
            return (byPerson.HourlyRate, byPerson.Currency);
        var fallback = rates.FirstOrDefault(r => r.UserId is null && r.StaffId is null && !string.IsNullOrWhiteSpace(r.Role))
                       ?? rates.FirstOrDefault();
        return fallback is null ? (0m, "USD") : (fallback.HourlyRate, fallback.Currency);
    }

    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static PayRateDTO MapRate(PayRateEntity r) => new()
    {
        Id = r.Id,
        StaffId = r.StaffId,
        UserId = r.UserId,
        Role = r.Role,
        HourlyRate = r.HourlyRate,
        Currency = r.Currency,
        EffectiveFrom = r.EffectiveFrom,
        CreatedAt = r.CreatedAt
    };

    private static PayPeriodDTO MapPeriod(PayPeriodEntity p) => new()
    {
        Id = p.Id,
        Label = p.Label,
        StartsOn = p.StartsOn,
        EndsOn = p.EndsOn,
        Status = p.Status,
        TotalMinutes = p.TotalMinutes,
        TotalAmount = p.TotalAmount,
        Currency = p.Currency,
        Lines = DeserializeLines(p.LinesJson),
        ApprovalId = p.ApprovalId,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };

    private static List<PayPeriodLineDTO> DeserializeLines(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<PayPeriodLineDTO>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
