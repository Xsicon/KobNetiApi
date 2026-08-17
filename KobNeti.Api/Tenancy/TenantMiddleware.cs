namespace KobNeti.Api.Tenancy;

public class TenantMiddleware
{
    public const string TenantKeyHeader = "X-Tenant-Key";

    private static readonly PathString[] SkipPrefixes =
    [
        new("/api/SupportAuth/exchange"),
        new("/swagger"),
        new("/health")
    ];

    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantResolver resolver,
        ITenantContextAccessor accessor)
    {
        var path = context.Request.Path;
        if (SkipPrefixes.Any(p => path.StartsWithSegments(p)))
        {
            await _next(context);
            return;
        }

        if (!path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(TenantKeyHeader, out var keyValues)
            || string.IsNullOrWhiteSpace(keyValues.FirstOrDefault()))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = $"Missing {TenantKeyHeader} header."
            });
            return;
        }

        if (!resolver.TryResolveByPublicKey(keyValues.First(), out var tenant) || tenant is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = "Unknown or disabled tenant key."
            });
            return;
        }

        accessor.Current = tenant;
        context.Items["TenantId"] = tenant.TenantId;
        await _next(context);
    }
}
