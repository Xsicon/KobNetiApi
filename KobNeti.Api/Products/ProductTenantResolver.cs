using KobNeti.Api.Tenancy;

namespace KobNeti.Api.Products;

/// <summary>
/// Resolves tenants from the Product Registry (DB or in-memory seed), with sync cache refresh.
/// </summary>
public class ProductTenantResolver : ITenantResolver
{
    private readonly IProductRegistry _registry;
    private readonly object _gate = new();
    private List<TenantContext> _cache = [];
    private DateTime _cacheAt = DateTime.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public ProductTenantResolver(IProductRegistry registry)
    {
        _registry = registry;
    }

    public bool TryResolveByPublicKey(string? publicKey, out TenantContext? tenant)
    {
        tenant = null;
        if (string.IsNullOrWhiteSpace(publicKey))
            return false;

        var key = publicKey.Trim();
        tenant = Snapshot().FirstOrDefault(t =>
            string.Equals(t.PublicKey, key, StringComparison.Ordinal));
        return tenant is not null;
    }

    public bool TryGetById(string tenantId, out TenantContext? tenant)
    {
        tenant = Snapshot().FirstOrDefault(t =>
            string.Equals(t.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));
        return tenant is not null;
    }

    public IReadOnlyList<TenantContext> ListEnabled() => Snapshot();

    public void InvalidateCache()
    {
        lock (_gate)
        {
            _cacheAt = DateTime.MinValue;
            _cache = [];
        }
    }

    private List<TenantContext> Snapshot()
    {
        lock (_gate)
        {
            if (DateTime.UtcNow - _cacheAt < CacheTtl && _cache.Count > 0)
                return _cache;
        }

        var products = _registry.ListEnabledAsync().GetAwaiter().GetResult();
        var mapped = products.Select(ToContext).ToList();

        lock (_gate)
        {
            _cache = mapped;
            _cacheAt = DateTime.UtcNow;
            return _cache;
        }
    }

    private static TenantContext ToContext(ProductRecord p) => new()
    {
        TenantId = p.Slug,
        DisplayName = p.DisplayName,
        PublicKey = p.PublicKey,
        JwtSecret = p.JwtSecret ?? "",
        UpstreamApiBaseUrl = p.UpstreamApiBaseUrl ?? "",
        PublicHelpCenterUrl = p.PublicHelpCenterUrl ?? ""
    };
}
