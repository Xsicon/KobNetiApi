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
                GithubRepoUrl = null,
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

    public Task<IReadOnlyList<ProductRecord>> ListAllAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<ProductRecord>>(
                _products.Select(p => Clone(p)!).ToList());
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

    public Task<ProductRecord?> UpdateGithubRepoUrlAsync(string slug, string? githubRepoUrl, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var hit = _products.FirstOrDefault(p =>
                string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));
            if (hit is null)
                return Task.FromResult<ProductRecord?>(null);

            hit.GithubRepoUrl = string.IsNullOrWhiteSpace(githubRepoUrl) ? null : githubRepoUrl.Trim();
            hit.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(Clone(hit));
        }
    }

    public Task<ProductRecord?> UpdateUpstreamApiBaseUrlAsync(string slug, string? upstreamApiBaseUrl, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var hit = _products.FirstOrDefault(p =>
                string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));
            if (hit is null)
                return Task.FromResult<ProductRecord?>(null);

            hit.UpstreamApiBaseUrl = string.IsNullOrWhiteSpace(upstreamApiBaseUrl)
                ? null
                : upstreamApiBaseUrl.Trim().TrimEnd('/');
            hit.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(Clone(hit));
        }
    }

    public Task<ProductRecord?> CreateAsync(ProductRecord product, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_products.Any(p => string.Equals(p.Slug, product.Slug, StringComparison.OrdinalIgnoreCase)))
                return Task.FromResult<ProductRecord?>(null);

            var now = DateTime.UtcNow;
            var created = Clone(product)!;
            created.Id = created.Id == Guid.Empty ? Guid.NewGuid() : created.Id;
            created.CreatedAt = now;
            created.UpdatedAt = now;
            created.Enabled = true;
            if (string.IsNullOrWhiteSpace(created.PublicKey))
                created.PublicKey = EmbedKeyHelper.GeneratePublicKey(created.Slug);
            _products.Add(created);
            return Task.FromResult<ProductRecord?>(Clone(created));
        }
    }

    public Task<ProductRecord?> UpdateCatalogAsync(string slug, ProductRecord patch, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var hit = _products.FirstOrDefault(p =>
                string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));
            if (hit is null)
                return Task.FromResult<ProductRecord?>(null);

            if (!string.IsNullOrWhiteSpace(patch.DisplayName))
                hit.DisplayName = patch.DisplayName.Trim();
            if (!string.IsNullOrWhiteSpace(patch.ProductType))
                hit.ProductType = patch.ProductType;
            if (!string.IsNullOrWhiteSpace(patch.Status))
            {
                hit.Status = patch.Status;
                hit.Enabled = !string.Equals(patch.Status, "deprecated", StringComparison.OrdinalIgnoreCase);
            }
            if (!string.IsNullOrWhiteSpace(patch.SupportTier))
                hit.SupportTier = patch.SupportTier;
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
                GithubRepoUrl = p.GithubRepoUrl,
                Enabled = p.Enabled,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            };
}
