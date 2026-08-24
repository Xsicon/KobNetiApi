using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Services;
using KobNeti.Api.Shared;
using KobNeti.Api.Tenancy;

namespace KobNeti.Api.Controllers;

[Route("api/TimeEntries")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
public class TimeEntriesController : ApiControllerBase
{
    private readonly ITimeTrackingService _time;
    private readonly ITenantContextAccessor _tenant;

    public TimeEntriesController(ITimeTrackingService time, ITenantContextAccessor tenant)
    {
        _time = time;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<TimeEntryDTO>>>> List(
        [FromQuery] Guid? userId = null,
        [FromQuery] string? status = null) =>
        HandleResponse(await _time.ListAsync(RequireTenantId(_tenant), userId, status));

    [HttpPost("clock-in")]
    public async Task<ActionResult<Response<TimeEntryDTO>>> ClockIn([FromBody] ClockInDTO? request) =>
        HandleResponse(await _time.ClockInAsync(
            RequireTenantId(_tenant),
            request ?? new ClockInDTO(),
            AdminRoleClaims.GetUserId(User),
            AdminRoleClaims.GetDisplayName(User)));

    [HttpPost("clock-out")]
    public async Task<ActionResult<Response<TimeEntryDTO>>> ClockOut() =>
        HandleResponse(await _time.ClockOutAsync(
            RequireTenantId(_tenant),
            AdminRoleClaims.GetUserId(User),
            AdminRoleClaims.GetDisplayName(User)));

    [HttpPost("manual")]
    public async Task<ActionResult<Response<TimeEntryDTO>>> Manual([FromBody] ManualTimeDTO request) =>
        HandleResponse(await _time.AddManualAsync(
            RequireTenantId(_tenant),
            request,
            AdminRoleClaims.GetUserId(User),
            AdminRoleClaims.GetDisplayName(User)));

    [HttpPost("request-edit")]
    public async Task<ActionResult<Response<ApprovalRequestDTO>>> RequestEdit([FromBody] RequestTimeEditDTO request) =>
        HandleResponse(await _time.RequestEditAsync(
            RequireTenantId(_tenant),
            request,
            AdminRoleClaims.GetUserId(User),
            AdminRoleClaims.GetDisplayName(User)));
}

[Route("api/Approvals")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
public class ApprovalsController : ApiControllerBase
{
    private readonly IApprovalService _approvals;
    private readonly ITenantContextAccessor _tenant;

    public ApprovalsController(IApprovalService approvals, ITenantContextAccessor tenant)
    {
        _approvals = approvals;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<ApprovalRequestDTO>>>> List([FromQuery] string? status = null) =>
        HandleResponse(await _approvals.ListAsync(RequireTenantId(_tenant), status));

    [HttpPost("{id:guid}/decide")]
    public async Task<ActionResult<Response<ApprovalRequestDTO>>> Decide(Guid id, [FromBody] DecideApprovalDTO request) =>
        HandleResponse(await _approvals.DecideAsync(
            RequireTenantId(_tenant),
            id,
            request,
            AdminRoleClaims.GetUserId(User),
            AdminRoleClaims.GetDisplayName(User)));
}

[Route("api/Payroll")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
public class PayrollController : ApiControllerBase
{
    private readonly IPayrollService _payroll;
    private readonly ITenantContextAccessor _tenant;

    public PayrollController(IPayrollService payroll, ITenantContextAccessor tenant)
    {
        _payroll = payroll;
        _tenant = tenant;
    }

    [HttpGet("rates")]
    public async Task<ActionResult<Response<List<PayRateDTO>>>> Rates() =>
        HandleResponse(await _payroll.ListRatesAsync(RequireTenantId(_tenant)));

    [HttpPost("rates")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<PayRateDTO>>> SaveRate([FromBody] SavePayRateDTO request) =>
        HandleResponse(await _payroll.SaveRateAsync(RequireTenantId(_tenant), request));

    [HttpGet("periods")]
    public async Task<ActionResult<Response<List<PayPeriodDTO>>>> Periods() =>
        HandleResponse(await _payroll.ListPeriodsAsync(RequireTenantId(_tenant)));

    [HttpPost("periods")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<PayPeriodDTO>>> CreatePeriod([FromBody] CreatePayPeriodDTO request) =>
        HandleResponse(await _payroll.CreatePeriodAsync(RequireTenantId(_tenant), request));

    [HttpPost("periods/{id:guid}/calculate")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<PayPeriodDTO>>> Calculate(Guid id) =>
        HandleResponse(await _payroll.CalculateAsync(RequireTenantId(_tenant), id));

    [HttpPost("periods/{id:guid}/submit")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<PayPeriodDTO>>> Submit(Guid id) =>
        HandleResponse(await _payroll.SubmitForApprovalAsync(
            RequireTenantId(_tenant),
            id,
            AdminRoleClaims.GetUserId(User),
            AdminRoleClaims.GetDisplayName(User)));

    [HttpGet("periods/{id:guid}/export.csv")]
    public async Task<IActionResult> ExportCsv(Guid id)
    {
        var result = await _payroll.ExportCsvAsync(RequireTenantId(_tenant), id);
        if (!result.Success)
            return BadRequest(result);
        return File(System.Text.Encoding.UTF8.GetBytes(result.Data ?? ""), "text/csv", $"payroll-{id:N}.csv");
    }
}
