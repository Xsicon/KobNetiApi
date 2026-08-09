using Microsoft.AspNetCore.Mvc;
using Sominnercore.SupportApi.Shared;
using Sominnercore.SupportApi.Tenancy;

namespace Sominnercore.SupportApi.Controllers;

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
