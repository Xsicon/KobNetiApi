namespace KobNeti.Api.Products;

public class ProductRepoRecord
{
    public Guid Id { get; set; }
    public string ProductSlug { get; set; } = string.Empty;
    public string RepoKind { get; set; } = ProductRepoKinds.WebApp;
    public string Title { get; set; } = string.Empty;
    public string GithubRepoUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public static class ProductRepoKinds
{
    public const string WebApp = "web_app";
    public const string Api = "api";
    public const string Mobile = "mobile";
    public const string Internal = "internal";

    public static readonly string[] All = [WebApp, Api, Mobile, Internal];

    public static bool IsValid(string? kind) =>
        !string.IsNullOrWhiteSpace(kind) &&
        All.Contains(kind.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string kind) =>
        All.First(k => k.Equals(kind.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string DefaultTitle(string displayName, string repoKind) => repoKind switch
    {
        Api => $"{displayName} API",
        Mobile => $"{displayName} Mobile",
        Internal => $"{displayName} Internal",
        _ => $"{displayName} Web"
    };
}

public interface IProductRepoRegistry
{
    Task<IReadOnlyList<ProductRepoRecord>> ListByProductSlugAsync(string productSlug, CancellationToken ct = default);
    Task<ProductRepoRecord?> UpsertAsync(string productSlug, string repoKind, string title, string githubRepoUrl, CancellationToken ct = default);
    Task<bool> DeleteAsync(string productSlug, string repoKind, CancellationToken ct = default);
}
