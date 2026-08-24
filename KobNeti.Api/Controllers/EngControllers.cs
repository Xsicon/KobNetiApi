using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Services;
using KobNeti.Api.Shared;
using KobNeti.Api.Tenancy;

namespace KobNeti.Api.Controllers;

[Route("api/EngTasks")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
public class EngTasksController : ApiControllerBase
{
    private readonly IEngTaskService _tasks;
    private readonly ITenantContextAccessor _tenant;

    public EngTasksController(IEngTaskService tasks, ITenantContextAccessor tenant)
    {
        _tasks = tasks;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<EngTaskDTO>>>> List(
        [FromQuery] string? status = null,
        [FromQuery] Guid? milestoneId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 100;
        return HandleResponse(await _tasks.ListAsync(RequireTenantId(_tenant), status, milestoneId, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Response<EngTaskDTO>>> Get(Guid id) =>
        HandleResponse(await _tasks.GetAsync(RequireTenantId(_tenant), id));

    [HttpPost]
    public async Task<ActionResult<Response<EngTaskDTO>>> Create([FromBody] CreateEngTaskDTO request) =>
        HandleResponse(await _tasks.CreateAsync(
            RequireTenantId(_tenant),
            request,
            AdminRoleClaims.GetDisplayName(User),
            AdminRoleClaims.GetUserId(User)));

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<Response<EngTaskDTO>>> Update(Guid id, [FromBody] UpdateEngTaskDTO request) =>
        HandleResponse(await _tasks.UpdateAsync(RequireTenantId(_tenant), id, request));
}

[Route("api/Milestones")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
public class MilestonesController : ApiControllerBase
{
    private readonly IMilestoneService _milestones;
    private readonly ITenantContextAccessor _tenant;

    public MilestonesController(IMilestoneService milestones, ITenantContextAccessor tenant)
    {
        _milestones = milestones;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<MilestoneDTO>>>> List() =>
        HandleResponse(await _milestones.ListAsync(RequireTenantId(_tenant)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Response<MilestoneDTO>>> Get(Guid id) =>
        HandleResponse(await _milestones.GetAsync(RequireTenantId(_tenant), id));

    [HttpPost]
    public async Task<ActionResult<Response<MilestoneDTO>>> Create([FromBody] CreateMilestoneDTO request) =>
        HandleResponse(await _milestones.CreateAsync(RequireTenantId(_tenant), request));

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<Response<MilestoneDTO>>> Update(Guid id, [FromBody] UpdateMilestoneDTO request) =>
        HandleResponse(await _milestones.UpdateAsync(RequireTenantId(_tenant), id, request));
}

[Route("api/Github")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
public class GithubController : ApiControllerBase
{
    private readonly IGithubReadService _github;
    private readonly ITenantContextAccessor _tenant;

    public GithubController(IGithubReadService github, ITenantContextAccessor tenant)
    {
        _github = github;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<Response<GithubCacheDTO>>> Get() =>
        HandleResponse(await _github.GetCachedAsync(RequireTenantId(_tenant)));

    [HttpPost("refresh")]
    public async Task<ActionResult<Response<GithubCacheDTO>>> Refresh(CancellationToken ct) =>
        HandleResponse(await _github.RefreshAsync(RequireTenantId(_tenant), ct));
}
