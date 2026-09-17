using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Services;
using KobNeti.Api.Shared;
using KobNeti.Api.Tenancy;

namespace KobNeti.Api.Controllers;

[Route("api/Overview")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
public class OverviewController : ApiControllerBase
{
    private readonly IOverviewService _overview;
    private readonly ITenantContextAccessor _tenant;

    public OverviewController(IOverviewService overview, ITenantContextAccessor tenant)
    {
        _overview = overview;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<Response<OverviewDTO>>> Get()
    {
        var ctx = _tenant.Current;
        var tenantId = RequireTenantId(_tenant);
        return HandleResponse(await _overview.GetAsync(
            tenantId, ctx?.DisplayName ?? tenantId, AdminRoleClaims.GetUserId(User)));
    }

    [HttpGet("cross-product")]
    public async Task<ActionResult<Response<CrossProductOverviewDTO>>> CrossProduct() =>
        HandleResponse(await _overview.GetCrossProductAsync(User));
}

[Route("api/Reports")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
public class ReportsController : ApiControllerBase
{
    private readonly IReportService _reports;
    private readonly ITenantContextAccessor _tenant;

    public ReportsController(IReportService reports, ITenantContextAccessor tenant)
    {
        _reports = reports;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<ReportRunDTO>>>> List() =>
        HandleResponse(await _reports.ListAsync(RequireTenantId(_tenant)));

    [HttpPost("run")]
    public async Task<ActionResult<Response<ReportRunDTO>>> Run([FromBody] RunReportDTO request) =>
        HandleResponse(await _reports.RunAsync(
            RequireTenantId(_tenant), request, AdminRoleClaims.GetDisplayName(User)));

    [HttpGet("{id:guid}/csv")]
    public async Task<ActionResult<Response<string>>> Csv(Guid id) =>
        HandleResponse(await _reports.GetCsvAsync(RequireTenantId(_tenant), id));
}

[Route("api/PlatformHelp")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
public class PlatformHelpController : ApiControllerBase
{
    private readonly IPlatformHelpService _help;

    public PlatformHelpController(IPlatformHelpService help) => _help = help;

    [HttpGet]
    public async Task<ActionResult<Response<List<PlatformHelpArticleDTO>>>> List([FromQuery] bool publishedOnly = true) =>
        HandleResponse(await _help.ListAsync(publishedOnly));

    [HttpGet("{slug}")]
    public async Task<ActionResult<Response<PlatformHelpArticleDTO>>> Get(string slug) =>
        HandleResponse(await _help.GetBySlugAsync(slug));

    [HttpPut]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<PlatformHelpArticleDTO>>> Upsert([FromBody] SavePlatformHelpDTO request) =>
        HandleResponse(await _help.UpsertAsync(request));

    [HttpPost("video")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(52_428_800)]
    [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
    public async Task<ActionResult<Response<PlatformHelpVideoDTO>>> UploadVideo(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(Response<PlatformHelpVideoDTO>.Fail("Video file is required"));
        await using var stream = file.OpenReadStream();
        return HandleResponse(await _help.UploadVideoAsync(file.FileName, file.ContentType, stream, file.Length));
    }
}

[Route("api/InternalChat")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
public class InternalChatController : ApiControllerBase
{
    private readonly IInternalChatService _chat;
    private readonly ITenantContextAccessor _tenant;

    public InternalChatController(IInternalChatService chat, ITenantContextAccessor tenant)
    {
        _chat = chat;
        _tenant = tenant;
    }

    [HttpGet("channels")]
    public async Task<ActionResult<Response<List<ImChannelDTO>>>> Channels() =>
        HandleResponse(await _chat.ListChannelsAsync(RequireTenantId(_tenant)));

    [HttpPost("channels")]
    public async Task<ActionResult<Response<ImChannelDTO>>> CreateChannel([FromBody] CreateImChannelDTO request) =>
        HandleResponse(await _chat.CreateChannelAsync(
            RequireTenantId(_tenant), request, AdminRoleClaims.GetUserId(User)));

    [HttpPost("dms")]
    public async Task<ActionResult<Response<ImChannelDTO>>> OpenDm([FromBody] OpenImDmDTO request) =>
        HandleResponse(await _chat.OpenDmAsync(
            RequireTenantId(_tenant),
            request,
            AdminRoleClaims.GetUserId(User),
            AdminRoleClaims.GetDisplayName(User)));

    [HttpGet("channels/{id:guid}/messages")]
    public async Task<ActionResult<Response<List<ImMessageDTO>>>> Messages(Guid id) =>
        HandleResponse(await _chat.ListMessagesAsync(RequireTenantId(_tenant), id));

    [HttpPost("channels/{id:guid}/messages")]
    public async Task<ActionResult<Response<ImMessageDTO>>> Send(Guid id, [FromBody] SendImMessageDTO request) =>
        HandleResponse(await _chat.SendAsync(
            RequireTenantId(_tenant), id, request,
            AdminRoleClaims.GetUserId(User),
            AdminRoleClaims.GetDisplayName(User)));
}

[Route("api/Assets")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
public class AssetsController : ApiControllerBase
{
    private readonly IAssetService _assets;
    private readonly ITenantContextAccessor _tenant;

    public AssetsController(IAssetService assets, ITenantContextAccessor tenant)
    {
        _assets = assets;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<AssetDTO>>>> List() =>
        HandleResponse(await _assets.ListAsync(RequireTenantId(_tenant)));

    [HttpPost]
    public async Task<ActionResult<Response<AssetDTO>>> Create([FromBody] SaveAssetDTO request) =>
        HandleResponse(await _assets.CreateAsync(RequireTenantId(_tenant), request));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Response<AssetDTO>>> Update(Guid id, [FromBody] SaveAssetDTO request) =>
        HandleResponse(await _assets.UpdateAsync(RequireTenantId(_tenant), id, request));

    [HttpPost("{id:guid}/assign")]
    public async Task<ActionResult<Response<AssetDTO>>> Assign(Guid id, [FromBody] AssignAssetDTO request) =>
        HandleResponse(await _assets.AssignAsync(RequireTenantId(_tenant), id, request));

    [HttpPost("{id:guid}/retire")]
    public async Task<ActionResult<Response<AssetDTO>>> Retire(Guid id) =>
        HandleResponse(await _assets.RetireAsync(RequireTenantId(_tenant), id));

    [HttpPost("renewal-reminders")]
    public async Task<ActionResult<Response<int>>> RenewalReminders([FromQuery] int withinDays = 30) =>
        HandleResponse(await _assets.SendRenewalRemindersAsync(RequireTenantId(_tenant), withinDays));
}
