using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Services;
using KobNeti.Api.Shared;
using KobNeti.Api.Tenancy;

namespace KobNeti.Api.Controllers;

[Route("api/Incidents")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
public class IncidentsController : ApiControllerBase
{
    private readonly IIncidentService _incidents;
    private readonly ITenantContextAccessor _tenant;

    public IncidentsController(IIncidentService incidents, ITenantContextAccessor tenant)
    {
        _incidents = incidents;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<IncidentDTO>>>> List(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 50;
        return HandleResponse(await _incidents.ListAsync(RequireTenantId(_tenant), status, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Response<IncidentDTO>>> Get(Guid id) =>
        HandleResponse(await _incidents.GetAsync(RequireTenantId(_tenant), id));

    [HttpPost]
    public async Task<ActionResult<Response<IncidentDTO>>> Create([FromBody] CreateIncidentDTO request) =>
        HandleResponse(await _incidents.CreateAsync(
            RequireTenantId(_tenant),
            request,
            AdminRoleClaims.GetDisplayName(User),
            AdminRoleClaims.GetUserId(User)));

    [HttpPost("from-ticket/{ticketId:guid}")]
    public async Task<ActionResult<Response<IncidentDTO>>> EscalateFromTicket(
        Guid ticketId,
        [FromBody] EscalateTicketDTO? request) =>
        HandleResponse(await _incidents.EscalateFromTicketAsync(
            RequireTenantId(_tenant),
            ticketId,
            request ?? new EscalateTicketDTO(),
            AdminRoleClaims.GetDisplayName(User),
            AdminRoleClaims.GetUserId(User)));

    [HttpPost("from-chat/{sessionId:guid}")]
    public async Task<ActionResult<Response<IncidentDTO>>> EscalateFromChat(
        Guid sessionId,
        [FromBody] EscalateFromChatDTO request) =>
        HandleResponse(await _incidents.EscalateFromChatAsync(
            RequireTenantId(_tenant),
            sessionId,
            request,
            AdminRoleClaims.GetDisplayName(User),
            AdminRoleClaims.GetUserId(User)));

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<Response<IncidentDTO>>> Update(Guid id, [FromBody] UpdateIncidentDTO request) =>
        HandleResponse(await _incidents.UpdateAsync(
            RequireTenantId(_tenant),
            id,
            request,
            AdminRoleClaims.GetDisplayName(User)));
}
