using Postgrest.Attributes;
using Postgrest.Models;
using static Postgrest.Constants;

namespace KobNeti.Api.Products;

[Table("product_repos")]
public class SbProductRepo : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("product_slug")] public string ProductSlug { get; set; } = string.Empty;
    [Column("repo_kind")] public string RepoKind { get; set; } = ProductRepoKinds.WebApp;
    [Column("title")] public string Title { get; set; } = string.Empty;
    [Column("github_repo_url")] public string GithubRepoUrl { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

public class SupabaseProductRepoRegistry : IProductRepoRegistry
{
    private readonly Supabase.Client _client;
    private readonly InMemoryProductRepoRegistry _fallback;
    private readonly ILogger<SupabaseProductRepoRegistry> _logger;

    public SupabaseProductRepoRegistry(
        Supabase.Client client,
        InMemoryProductRepoRegistry fallback,
        ILogger<SupabaseProductRepoRegistry> logger)
    {
        _client = client;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProductRepoRecord>> ListByProductSlugAsync(string productSlug, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.From<SbProductRepo>()
                .Filter("product_slug", Operator.Equals, productSlug)
                .Order("repo_kind", Ordering.Ascending)
                .Get();
            var rows = (response.Models ?? []).Select(ToRecord).ToList();
            return rows.Count > 0 ? rows : await _fallback.ListByProductSlugAsync(productSlug, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product repos list failed for {Slug}", productSlug);
            return await _fallback.ListByProductSlugAsync(productSlug, ct);
        }
    }

    public async Task<ProductRepoRecord?> UpsertAsync(
        string productSlug,
        string repoKind,
        string title,
        string githubRepoUrl,
        CancellationToken ct = default)
    {
        var kind = ProductRepoKinds.Normalize(repoKind);
        var slug = productSlug.Trim();
        var now = DateTime.UtcNow;

        try
        {
            var existing = await _client.From<SbProductRepo>()
                .Filter("product_slug", Operator.Equals, slug)
                .Filter("repo_kind", Operator.Equals, kind)
                .Get();
            var row = (existing.Models ?? []).FirstOrDefault();

            if (row is null)
            {
                row = new SbProductRepo
                {
                    Id = Guid.NewGuid(),
                    ProductSlug = slug,
                    RepoKind = kind,
                    Title = title.Trim(),
                    GithubRepoUrl = githubRepoUrl.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now
                };
                var inserted = await _client.From<SbProductRepo>().Insert(row);
                row = inserted.Models.FirstOrDefault() ?? row;
            }
            else
            {
                row.Title = title.Trim();
                row.GithubRepoUrl = githubRepoUrl.Trim();
                row.UpdatedAt = now;
                await _client.From<SbProductRepo>()
                    .Filter("id", Operator.Equals, row.Id.ToString())
                    .Update(row);
            }

            return ToRecord(row);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product repo upsert failed for {Slug}/{Kind}", slug, kind);
            return await _fallback.UpsertAsync(slug, kind, title, githubRepoUrl, ct);
        }
    }

    public async Task<bool> DeleteAsync(string productSlug, string repoKind, CancellationToken ct = default)
    {
        var kind = ProductRepoKinds.Normalize(repoKind);
        try
        {
            await _client.From<SbProductRepo>()
                .Filter("product_slug", Operator.Equals, productSlug)
                .Filter("repo_kind", Operator.Equals, kind)
                .Delete();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product repo delete failed for {Slug}/{Kind}", productSlug, kind);
            return await _fallback.DeleteAsync(productSlug, kind, ct);
        }
    }

    private static ProductRepoRecord ToRecord(SbProductRepo m) => new()
    {
        Id = m.Id,
        ProductSlug = m.ProductSlug,
        RepoKind = m.RepoKind,
        Title = m.Title,
        GithubRepoUrl = m.GithubRepoUrl,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };
}
