using Microsoft.Extensions.Options;
using KobNeti.Api.Tenancy;

namespace KobNeti.Api.Products;

/// <summary>
/// Seeds from Support:Tenants config. Used when Supabase is not configured (dev/tests).
/// </summary>
public class InMemoryProductRegistry : IProductRegistry
{
    private readonly List<ProductRecord> _products;
    private readonly object _gate = new();

    public InMemoryProductRegistry(IOptions<SupportOptions> options)
    {
        var now = DateTime.UtcNow;
        _products = options.Value.Tenants
            .Where(kv => kv.Value.Enabled && !string.IsNullOrWhiteSpace(kv.Value.PublicKey))
            .Select(kv => new ProductRecord
            {
                Id = Guid.NewGuid(),
                Slug = kv.Key,
                DisplayName = string.IsNullOrWhiteSpace(kv.Value.DisplayName) ? kv.Key : kv.Value.DisplayName,
                ProductType = "saas_app",
                Status = "active",
                SupportTier = "standard",
                PublicKey = kv.Value.PublicKey,
                JwtSecret = kv.Value.JwtSecret,
                UpstreamApiBaseUrl = kv.Value.UpstreamApiBaseUrl,
                PublicHelpCenterUrl = kv.Value.PublicHelpCenterUrl,
                Enabled = kv.Value.Enabled,
                CreatedAt = now,
                UpdatedAt = now
            })
            .ToList();
    }

    public Task<IReadOnlyList<ProductRecord>> ListEnabledAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<ProductRecord>>(
                _products.Where(p => p.Enabled).Select(p => Clone(p)!).ToList());
        }
    }

    public Task<ProductRecord?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(Clone(_products.FirstOrDefault(p =>
                p.Enabled && string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase))));
        }
    }

    public Task<ProductRecord?> GetByPublicKeyAsync(string publicKey, CancellationToken ct = default)
    {
        var key = publicKey.Trim();
        lock (_gate)
        {
            return Task.FromResult(Clone(_products.FirstOrDefault(p =>
                p.Enabled && string.Equals(p.PublicKey, key, StringComparison.Ordinal))));
        }
    }

    public Task<ProductRecord?> RotatePublicKeyAsync(string slug, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var hit = _products.FirstOrDefault(p =>
                string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));
            if (hit is null)
                return Task.FromResult<ProductRecord?>(null);

            hit.PublicKey = EmbedKeyHelper.GeneratePublicKey(hit.Slug);
            hit.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(Clone(hit));
        }
    }

    private static ProductRecord? Clone(ProductRecord? p) =>
        p is null
            ? null
            : new ProductRecord
            {
                Id = p.Id,
                Slug = p.Slug,
                DisplayName = p.DisplayName,
                ProductType = p.ProductType,
                Status = p.Status,
                SupportTier = p.SupportTier,
                PublicKey = p.PublicKey,
                JwtSecret = p.JwtSecret,
                UpstreamApiBaseUrl = p.UpstreamApiBaseUrl,
                PublicHelpCenterUrl = p.PublicHelpCenterUrl,
                Enabled = p.Enabled,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            };
}
