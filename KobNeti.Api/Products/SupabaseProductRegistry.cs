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

            return OverlayConfigSecrets(rows);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product registry DB read failed; using config tenants.");
            return await _configFallback.ListEnabledAsync(ct);
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
        Enabled = m.Enabled,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };
}
