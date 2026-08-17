using Microsoft.AspNetCore.Mvc;
using KobNeti.Api.Shared;
using KobNeti.Api.Tenancy;

namespace KobNeti.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected string RequireTenantId(ITenantContextAccessor accessor)
    {
        var id = accessor.Current?.TenantId;
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("Tenant context missing.");
        return id;
    }

    protected ActionResult<Response<T>> HandleResponse<T>(Response<T> result)
    {
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}
