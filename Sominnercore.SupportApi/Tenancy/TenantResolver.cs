using Microsoft.Extensions.Options;

namespace Sominnercore.SupportApi.Tenancy;

public interface ITenantResolver
{
    bool TryResolveByPublicKey(string? publicKey, out TenantContext? tenant);
    bool TryGetById(string tenantId, out TenantContext? tenant);
    IReadOnlyList<TenantContext> ListEnabled();
}

public class TenantResolver : ITenantResolver
{
    private readonly SupportOptions _options;

    public TenantResolver(IOptions<SupportOptions> options)
    {
        _options = options.Value;
    }

    public bool TryResolveByPublicKey(string? publicKey, out TenantContext? tenant)
    {
        tenant = null;
        if (string.IsNullOrWhiteSpace(publicKey))
            return false;

        foreach (var (id, cfg) in _options.Tenants)
        {
            if (!cfg.Enabled || string.IsNullOrWhiteSpace(cfg.PublicKey))
                continue;

            if (!string.Equals(cfg.PublicKey, publicKey.Trim(), StringComparison.Ordinal))
                continue;

            tenant = ToContext(id, cfg);
            return true;
        }

        return false;
    }

    public bool TryGetById(string tenantId, out TenantContext? tenant)
    {
        tenant = null;
        if (!_options.Tenants.TryGetValue(tenantId, out var cfg) || !cfg.Enabled)
            return false;

        tenant = ToContext(tenantId, cfg);
        return true;
    }

    public IReadOnlyList<TenantContext> ListEnabled() =>
        _options.Tenants
            .Where(kv => kv.Value.Enabled)
            .Select(kv => ToContext(kv.Key, kv.Value))
            .ToList();

    private static TenantContext ToContext(string id, TenantConfig cfg) => new()
    {
        TenantId = id,
        DisplayName = cfg.DisplayName,
        PublicKey = cfg.PublicKey,
        JwtSecret = cfg.JwtSecret,
        UpstreamApiBaseUrl = cfg.UpstreamApiBaseUrl,
        PublicHelpCenterUrl = cfg.PublicHelpCenterUrl
    };
}
