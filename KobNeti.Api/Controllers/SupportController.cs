using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Services;
using KobNeti.Api.Shared;
using KobNeti.Api.Tenancy;

namespace KobNeti.Api.Controllers;

[Route("api/Support")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
public class SupportController : ApiControllerBase
{
    private readonly ISupportCountsService _counts;
    private readonly IMacroService _macros;
    private readonly ITenantContextAccessor _tenant;
    private readonly ITenantResolver _resolver;

    public SupportController(
        ISupportCountsService counts,
        IMacroService macros,
        ITenantContextAccessor tenant,
        ITenantResolver resolver)
    {
        _counts = counts;
        _macros = macros;
        _tenant = tenant;
        _resolver = resolver;
    }

    [HttpGet("counts")]
    public async Task<ActionResult<Response<SupportCountsDTO>>> GetCounts() =>
        HandleResponse(await _counts.GetCountsAsync(RequireTenantId(_tenant)));

    [HttpGet("tenants")]
    public ActionResult<Response<List<SupportTenantDTO>>> GetTenants()
    {
        var list = _resolver.ListEnabled()
            .Where(t => AdminRoleClaims.CanAccessProduct(User, t.TenantId))
            .Select(t => new SupportTenantDTO
            {
                TenantId = t.TenantId,
                DisplayName = t.DisplayName,
                PublicKey = t.PublicKey,
                PublicHelpCenterUrl = t.PublicHelpCenterUrl
            })
            .ToList();
        return Ok(Response<List<SupportTenantDTO>>.SuccessResponse(list, "Tenants loaded"));
    }

    [HttpGet("macros")]
    public async Task<ActionResult<Response<List<SupportMacroDTO>>>> ListMacros() =>
        HandleResponse(await _macros.ListAsync(RequireTenantId(_tenant)));

    [HttpPost("macros")]
    public async Task<ActionResult<Response<SupportMacroDTO>>> CreateMacro([FromBody] SaveMacroDTO request) =>
        HandleResponse(await _macros.CreateAsync(RequireTenantId(_tenant), request));

    [HttpPut("macros/{id:guid}")]
    public async Task<ActionResult<Response<SupportMacroDTO>>> UpdateMacro(Guid id, [FromBody] SaveMacroDTO request) =>
        HandleResponse(await _macros.UpdateAsync(RequireTenantId(_tenant), id, request));

    [HttpDelete("macros/{id:guid}")]
    public async Task<ActionResult<Response<bool>>> DeleteMacro(Guid id) =>
        HandleResponse(await _macros.DeleteAsync(RequireTenantId(_tenant), id));
}
