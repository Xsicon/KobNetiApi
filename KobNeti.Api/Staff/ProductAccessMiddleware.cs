using System.Security.Claims;
using KobNeti.Api.Auth;
using KobNeti.Api.Tenancy;

namespace KobNeti.Api.Staff;

/// <summary>
/// After authentication, ensure the agent JWT may access the product resolved from X-Tenant-Key.
/// Public (anonymous) calls are unchanged.
/// </summary>
public class ProductAccessMiddleware
{
    private readonly RequestDelegate _next;

    public ProductAccessMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantContextAccessor accessor)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true && accessor.Current is { } tenant)
        {
            if (!AdminRoleClaims.CanAccessProduct(user, tenant.TenantId))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = $"No access to product '{tenant.TenantId}'."
                });
                return;
            }
        }

        await _next(context);
    }
}
