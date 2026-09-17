using Microsoft.Extensions.Options;
using Postgrest.Attributes;
using Postgrest.Models;
using KobNeti.Api.Tenancy;
using static Postgrest.Constants;

namespace KobNeti.Api.Products;

[Table("products")]
public class SbProduct : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("slug")] public string Slug { get; set; } = string.Empty;
    [Column("display_name")] public string DisplayName { get; set; } = string.Empty;
    [Column("product_type")] public string ProductType { get; set; } = "saas_app";
    [Column("status")] public string Status { get; set; } = "active";
    [Column("support_tier")] public string SupportTier { get; set; } = "standard";
    [Column("public_key")] public string PublicKey { get; set; } = string.Empty;
    [Column("jwt_secret")] public string? JwtSecret { get; set; }
    [Column("upstream_api_base_url")] public string? UpstreamApiBaseUrl { get; set; }
    [Column("public_help_center_url")] public string? PublicHelpCenterUrl { get; set; }
    [Column("github_repo_url")] public string? GithubRepoUrl { get; set; }
    [Column("enabled")] public bool Enabled { get; set; } = true;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Loads products from sominnercore.products. Falls back to config when the table is empty or unreachable.
/// Config JwtSecret / UpstreamApiBaseUrl overlay DB values when set (secrets stay in env).
/// </summary>
public class SupabaseProductRegistry : IProductRegistry
{
    private readonly Supabase.Client _client;
    private readonly SupportOptions _options;
    private readonly InMemoryProductRegistry _configFallback;
    private readonly ILogger<SupabaseProductRegistry> _logger;

    public SupabaseProductRegistry(
        Supabase.Client client,
        IOptions<SupportOptions> options,
        InMemoryProductRegistry configFallback,
        ILogger<SupabaseProductRegistry> logger)
    {
        _client = client;
        _options = options.Value;
        _configFallback = configFallback;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProductRecord>> ListEnabledAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _client.From<SbProduct>()
                .Filter("enabled", Operator.Equals, "true")
                .Order("display_name", Ordering.Ascending)
                .Get();

            var rows = (response.Models ?? []).Select(ToRecord).ToList();
            if (rows.Count == 0)
                return await _configFallback.ListEnabledAsync(ct);

            return await MergeConfigTenantsAsync(OverlayConfigSecrets(rows).ToList(), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product registry DB read failed; using config tenants.");
            return await _configFallback.ListEnabledAsync(ct);
        }
    }

    public async Task<IReadOnlyList<ProductRecord>> ListAllAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _client.From<SbProduct>()
                .Order("display_name", Ordering.Ascending)
                .Get();

            var rows = (response.Models ?? []).Select(ToRecord).ToList();
            if (rows.Count == 0)
                return await _configFallback.ListAllAsync(ct);

            return await MergeConfigTenantsAsync(OverlayConfigSecrets(rows).ToList(), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product registry ListAll failed; using config tenants.");
            return await _configFallback.ListAllAsync(ct);
        }
    }

    public async Task<ProductRecord?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.From<SbProduct>()
                .Filter("slug", Operator.Equals, slug)
                .Filter("enabled", Operator.Equals, "true")
                .Get();

            var row = (response.Models ?? []).FirstOrDefault();
            if (row is null)
                return await _configFallback.GetBySlugAsync(slug, ct);

            return OverlayConfigSecrets([ToRecord(row)]).First();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product registry GetBySlug failed for {Slug}", slug);
            return await _configFallback.GetBySlugAsync(slug, ct);
        }
    }

    public async Task<ProductRecord?> GetByPublicKeyAsync(string publicKey, CancellationToken ct = default)
    {
        var key = publicKey.Trim();
        try
        {
            var response = await _client.From<SbProduct>()
                .Filter("public_key", Operator.Equals, key)
                .Filter("enabled", Operator.Equals, "true")
                .Get();

            var row = (response.Models ?? []).FirstOrDefault();
            if (row is null)
                return await _configFallback.GetByPublicKeyAsync(key, ct);

            return OverlayConfigSecrets([ToRecord(row)]).First();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product registry GetByPublicKey failed");
            return await _configFallback.GetByPublicKeyAsync(key, ct);
        }
    }

    public async Task<ProductRecord?> RotatePublicKeyAsync(string slug, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.From<SbProduct>()
                .Filter("slug", Operator.Equals, slug)
                .Get();

            var row = (response.Models ?? []).FirstOrDefault();
            if (row is null)
                return await _configFallback.RotatePublicKeyAsync(slug, ct);

            row.PublicKey = EmbedKeyHelper.GeneratePublicKey(row.Slug);
            row.UpdatedAt = DateTime.UtcNow;
            await _client.From<SbProduct>()
                .Filter("id", Operator.Equals, row.Id.ToString())
                .Update(row);

            return OverlayConfigSecrets([ToRecord(row)]).First();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product registry RotatePublicKey failed for {Slug}", slug);
            return await _configFallback.RotatePublicKeyAsync(slug, ct);
        }
    }

    public async Task<ProductRecord?> UpdateGithubRepoUrlAsync(string slug, string? githubRepoUrl, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.From<SbProduct>()
                .Filter("slug", Operator.Equals, slug)
                .Get();

            var row = (response.Models ?? []).FirstOrDefault();
            if (row is null)
                return await _configFallback.UpdateGithubRepoUrlAsync(slug, githubRepoUrl, ct);

            row.GithubRepoUrl = string.IsNullOrWhiteSpace(githubRepoUrl) ? null : githubRepoUrl.Trim();
            row.UpdatedAt = DateTime.UtcNow;
            await _client.From<SbProduct>()
                .Filter("id", Operator.Equals, row.Id.ToString())
                .Update(row);

            return OverlayConfigSecrets([ToRecord(row)]).First();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product registry UpdateGithubRepoUrl failed for {Slug}", slug);
            return await _configFallback.UpdateGithubRepoUrlAsync(slug, githubRepoUrl, ct);
        }
    }

    public async Task<ProductRecord?> UpdateUpstreamApiBaseUrlAsync(string slug, string? upstreamApiBaseUrl, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.From<SbProduct>()
                .Filter("slug", Operator.Equals, slug)
                .Get();

            var row = (response.Models ?? []).FirstOrDefault();
            if (row is null)
                return await _configFallback.UpdateUpstreamApiBaseUrlAsync(slug, upstreamApiBaseUrl, ct);

            row.UpstreamApiBaseUrl = string.IsNullOrWhiteSpace(upstreamApiBaseUrl)
                ? null
                : upstreamApiBaseUrl.Trim().TrimEnd('/');
            row.UpdatedAt = DateTime.UtcNow;
            await _client.From<SbProduct>()
                .Filter("id", Operator.Equals, row.Id.ToString())
                .Update(row);

            return OverlayConfigSecrets([ToRecord(row)]).First();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product registry UpdateUpstreamApiBaseUrl failed for {Slug}", slug);
            return await _configFallback.UpdateUpstreamApiBaseUrlAsync(slug, upstreamApiBaseUrl, ct);
        }
    }

    public async Task<ProductRecord?> CreateAsync(ProductRecord product, CancellationToken ct = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var row = new SbProduct
            {
                Id = product.Id == Guid.Empty ? Guid.NewGuid() : product.Id,
                Slug = product.Slug.Trim().ToLowerInvariant(),
                DisplayName = product.DisplayName.Trim(),
                ProductType = product.ProductType,
                Status = string.IsNullOrWhiteSpace(product.Status) ? "active" : product.Status,
                SupportTier = product.SupportTier,
                PublicKey = string.IsNullOrWhiteSpace(product.PublicKey)
                    ? EmbedKeyHelper.GeneratePublicKey(product.Slug)
                    : product.PublicKey,
                Enabled = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            var response = await _client.From<SbProduct>().Insert(row);
            var saved = (response.Models ?? []).FirstOrDefault() ?? row;
            return OverlayConfigSecrets([ToRecord(saved)]).First();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product registry Create failed for {Slug}", product.Slug);
            return await _configFallback.CreateAsync(product, ct);
        }
    }

    public async Task<ProductRecord?> UpdateCatalogAsync(string slug, ProductRecord patch, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.From<SbProduct>()
                .Filter("slug", Operator.Equals, slug)
                .Get();
            var row = (response.Models ?? []).FirstOrDefault();
            if (row is null)
                return await _configFallback.UpdateCatalogAsync(slug, patch, ct);

            if (!string.IsNullOrWhiteSpace(patch.DisplayName))
                row.DisplayName = patch.DisplayName.Trim();
            if (!string.IsNullOrWhiteSpace(patch.ProductType))
                row.ProductType = patch.ProductType;
            if (!string.IsNullOrWhiteSpace(patch.Status))
            {
                row.Status = patch.Status;
                row.Enabled = !string.Equals(patch.Status, "deprecated", StringComparison.OrdinalIgnoreCase);
            }
            if (!string.IsNullOrWhiteSpace(patch.SupportTier))
                row.SupportTier = patch.SupportTier;
            row.UpdatedAt = DateTime.UtcNow;
            await _client.From<SbProduct>()
                .Filter("id", Operator.Equals, row.Id.ToString())
                .Update(row);
            return OverlayConfigSecrets([ToRecord(row)]).First();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product registry UpdateCatalog failed for {Slug}", slug);
            return await _configFallback.UpdateCatalogAsync(slug, patch, ct);
        }
    }

    private async Task<IReadOnlyList<ProductRecord>> MergeConfigTenantsAsync(
        List<ProductRecord> rows, CancellationToken ct)
    {
        var extras = await _configFallback.ListEnabledAsync(ct);
        foreach (var extra in extras)
        {
            if (rows.Any(r => string.Equals(r.Slug, extra.Slug, StringComparison.OrdinalIgnoreCase)))
                continue;
            rows.Add(extra);
        }
        return rows;
    }

    private IReadOnlyList<ProductRecord> OverlayConfigSecrets(List<ProductRecord> rows)
    {
        foreach (var row in rows)
        {
            if (!_options.Tenants.TryGetValue(row.Slug, out var cfg))
                continue;

            if (!string.IsNullOrWhiteSpace(cfg.JwtSecret))
                row.JwtSecret = cfg.JwtSecret;
            if (!string.IsNullOrWhiteSpace(cfg.UpstreamApiBaseUrl))
                row.UpstreamApiBaseUrl = cfg.UpstreamApiBaseUrl;
            if (string.IsNullOrWhiteSpace(row.PublicHelpCenterUrl) &&
                !string.IsNullOrWhiteSpace(cfg.PublicHelpCenterUrl))
                row.PublicHelpCenterUrl = cfg.PublicHelpCenterUrl;
        }

        return rows;
    }

    private static ProductRecord ToRecord(SbProduct m) => new()
    {
        Id = m.Id,
        Slug = m.Slug,
        DisplayName = m.DisplayName,
        ProductType = m.ProductType,
        Status = m.Status,
        SupportTier = m.SupportTier,
        PublicKey = m.PublicKey,
        JwtSecret = m.JwtSecret,
        UpstreamApiBaseUrl = m.UpstreamApiBaseUrl,
        PublicHelpCenterUrl = m.PublicHelpCenterUrl,
        GithubRepoUrl = m.GithubRepoUrl,
        Enabled = m.Enabled,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };
}
