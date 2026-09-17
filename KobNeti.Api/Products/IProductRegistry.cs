namespace KobNeti.Api.Products;

public class ProductRecord
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty; // tenant_id
    public string DisplayName { get; set; } = string.Empty;
    public string ProductType { get; set; } = "saas_app";
    public string Status { get; set; } = "active";
    public string SupportTier { get; set; } = "standard";
    public string PublicKey { get; set; } = string.Empty;
    public string? JwtSecret { get; set; }
    public string? UpstreamApiBaseUrl { get; set; }
    public string? PublicHelpCenterUrl { get; set; }
    public string? GithubRepoUrl { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public interface IProductRegistry
{
    Task<IReadOnlyList<ProductRecord>> ListEnabledAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProductRecord>> ListAllAsync(CancellationToken ct = default);
    Task<ProductRecord?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<ProductRecord?> GetByPublicKeyAsync(string publicKey, CancellationToken ct = default);
    Task<ProductRecord?> RotatePublicKeyAsync(string slug, CancellationToken ct = default);
    Task<ProductRecord?> UpdateGithubRepoUrlAsync(string slug, string? githubRepoUrl, CancellationToken ct = default);
    Task<ProductRecord?> UpdateUpstreamApiBaseUrlAsync(string slug, string? upstreamApiBaseUrl, CancellationToken ct = default);
    Task<ProductRecord?> CreateAsync(ProductRecord product, CancellationToken ct = default);
    Task<ProductRecord?> UpdateCatalogAsync(string slug, ProductRecord patch, CancellationToken ct = default);
}

public static class ProductCatalog
{
    public static readonly string[] Types = ["public_website", "saas_app", "mobile_app", "internal_tool"];
    public static readonly string[] Statuses = ["active", "beta", "deprecated"];
    public static readonly string[] Tiers = ["standard", "priority", "enterprise"];

    public static string NormalizeType(string? value)
    {
        var v = (value ?? "").Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        return v switch
        {
            "public_website" or "website" => "public_website",
            "saas_app" or "web_app" or "saas" => "saas_app",
            "mobile_app" or "mobile" => "mobile_app",
            "internal_tool" or "internal" => "internal_tool",
            _ => ""
        };
    }

    public static string NormalizeStatus(string? value)
    {
        var v = (value ?? "").Trim().ToLowerInvariant();
        return Statuses.Contains(v) ? v : "";
    }

    public static string NormalizeTier(string? value)
    {
        var v = (value ?? "").Trim().ToLowerInvariant();
        return Tiers.Contains(v) ? v : "";
    }

    public static string NormalizeSlug(string? value)
    {
        var raw = (value ?? "").Trim().ToLowerInvariant();
        var chars = raw.Select(ch =>
                char.IsLetterOrDigit(ch) ? ch :
                ch is ' ' or '_' or '-' ? '-' : '\0')
            .Where(ch => ch != '\0')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return slug.Trim('-');
    }

    public static bool IsValidSlug(string slug) =>
        !string.IsNullOrWhiteSpace(slug)
        && slug.Length <= 64
        && slug.All(ch => char.IsLetterOrDigit(ch) || ch == '-')
        && !slug.StartsWith('-')
        && !slug.EndsWith('-');
}

public static class EmbedKeyHelper
{
    public static string GeneratePublicKey(string slug)
    {
        var safe = string.Join("", (slug ?? "product")
            .Trim()
            .ToLowerInvariant()
            .Where(ch => char.IsLetterOrDigit(ch) || ch == '-'));
        if (string.IsNullOrWhiteSpace(safe))
            safe = "product";
        return $"pk_{safe}_{Guid.NewGuid():N}";
    }

    public static string BuildWidgetSnippet(string publicKey, string apiBaseUrl = "https://YOUR-KOBNETI-API.onrender.com")
    {
        var baseUrl = (apiBaseUrl ?? "").TrimEnd('/');
        return $$"""
               <script
                 src="{{baseUrl}}/widget/support.js"
                 data-tenant-key="{{publicKey}}"
                 data-api-base="{{baseUrl}}"
                 async></script>
               """;
    }
}
