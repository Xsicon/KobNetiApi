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
}
