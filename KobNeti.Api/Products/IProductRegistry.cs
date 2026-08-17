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
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public interface IProductRegistry
{
    Task<IReadOnlyList<ProductRecord>> ListEnabledAsync(CancellationToken ct = default);
    Task<ProductRecord?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<ProductRecord?> GetByPublicKeyAsync(string publicKey, CancellationToken ct = default);
    Task<ProductRecord?> RotatePublicKeyAsync(string slug, CancellationToken ct = default);
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
