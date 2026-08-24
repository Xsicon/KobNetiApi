using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Services;
using KobNeti.Api.Shared;
using KobNeti.Api.Tenancy;

namespace KobNeti.Api.Controllers;

[Route("api/Audit")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
public class AuditController : ApiControllerBase
{
    private readonly IAuditService _audit;
    private readonly ITenantContextAccessor _tenant;

    public AuditController(IAuditService audit, ITenantContextAccessor tenant)
    {
        _audit = audit;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<AuditEventDTO>>>> Search(
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string? search = null,
        [FromQuery] int take = 100) =>
        HandleResponse(await _audit.SearchAsync(RequireTenantId(_tenant), action, entityType, search, Math.Clamp(take, 1, 500)));
}

[Route("api/Notifications")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
public class NotificationsController : ApiControllerBase
{
    private readonly IAppNotificationService _notifications;
    private readonly ITenantContextAccessor _tenant;

    public NotificationsController(IAppNotificationService notifications, ITenantContextAccessor tenant)
    {
        _notifications = notifications;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<NotificationDTO>>>> List([FromQuery] bool unreadOnly = false) =>
        HandleResponse(await _notifications.ListAsync(
            RequireTenantId(_tenant), AdminRoleClaims.GetUserId(User), unreadOnly));

    [HttpPost("{id:guid}/read")]
    public async Task<ActionResult<Response<NotificationDTO>>> MarkRead(Guid id) =>
        HandleResponse(await _notifications.MarkReadAsync(RequireTenantId(_tenant), id));

    [HttpGet("preferences")]
    public async Task<ActionResult<Response<NotificationPrefsDTO>>> GetPrefs()
    {
        var userId = AdminRoleClaims.GetUserId(User);
        if (userId is null)
            return BadRequest(Response<NotificationPrefsDTO>.Fail("User id required"));
        return HandleResponse(await _notifications.GetPrefsAsync(RequireTenantId(_tenant), userId.Value));
    }

    [HttpPut("preferences")]
    public async Task<ActionResult<Response<NotificationPrefsDTO>>> SavePrefs([FromBody] NotificationPrefsDTO prefs)
    {
        var userId = AdminRoleClaims.GetUserId(User);
        if (userId is null)
            return BadRequest(Response<NotificationPrefsDTO>.Fail("User id required"));
        return HandleResponse(await _notifications.SavePrefsAsync(RequireTenantId(_tenant), userId.Value, prefs));
    }
}

[Route("api/Calendar")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
public class CalendarController : ApiControllerBase
{
    private readonly ICalendarOpsService _calendar;
    private readonly ITenantContextAccessor _tenant;

    public CalendarController(ICalendarOpsService calendar, ITenantContextAccessor tenant)
    {
        _calendar = calendar;
        _tenant = tenant;
    }

    [HttpGet("events")]
    public async Task<ActionResult<Response<List<CalendarEventDTO>>>> List() =>
        HandleResponse(await _calendar.ListAsync(RequireTenantId(_tenant)));

    [HttpPost("events")]
    public async Task<ActionResult<Response<CalendarEventDTO>>> Create([FromBody] CreateCalendarEventDTO request) =>
        HandleResponse(await _calendar.CreateAsync(RequireTenantId(_tenant), request));

    [HttpPost("reminders")]
    public async Task<ActionResult<Response<int>>> Reminders([FromQuery] int withinHours = 48) =>
        HandleResponse(await _calendar.SendRemindersAsync(RequireTenantId(_tenant), withinHours));
}

[Route("api/OpsFiles")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
public class OpsFilesController : ApiControllerBase
{
    private readonly IOpsFileService _files;
    private readonly ITenantContextAccessor _tenant;

    public OpsFilesController(IOpsFileService files, ITenantContextAccessor tenant)
    {
        _files = files;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<OpsFileDTO>>>> List([FromQuery] string? folder = null) =>
        HandleResponse(await _files.ListAsync(RequireTenantId(_tenant), folder));

    [HttpPost]
    public async Task<ActionResult<Response<OpsFileDTO>>> Create([FromBody] CreateOpsFileDTO request) =>
        HandleResponse(await _files.CreateAsync(
            RequireTenantId(_tenant), request,
            AdminRoleClaims.GetUserId(User),
            AdminRoleClaims.GetDisplayName(User)));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<Response<object>>> Delete(Guid id) =>
        HandleResponse(await _files.DeleteAsync(RequireTenantId(_tenant), id));
}

[Route("api/Integrations")]
[Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
public class IntegrationsController : ApiControllerBase
{
    private readonly IIntegrationService _integrations;
    private readonly ITenantContextAccessor _tenant;

    public IntegrationsController(IIntegrationService integrations, ITenantContextAccessor tenant)
    {
        _integrations = integrations;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<IntegrationDTO>>>> List() =>
        HandleResponse(await _integrations.ListAsync(RequireTenantId(_tenant)));

    [HttpPost("connect")]
    public async Task<ActionResult<Response<IntegrationDTO>>> Connect([FromBody] ConnectIntegrationDTO request) =>
        HandleResponse(await _integrations.ConnectAsync(RequireTenantId(_tenant), request));

    [HttpPost("{provider}/disconnect")]
    public async Task<ActionResult<Response<IntegrationDTO>>> Disconnect(string provider) =>
        HandleResponse(await _integrations.DisconnectAsync(RequireTenantId(_tenant), provider));
}
