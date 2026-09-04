namespace KobNeti.Api.Products;

/// <summary>
/// In-memory linked repos for dev/tests. Products like MuuqWear have a web app repo and an API repo.
/// </summary>
public class InMemoryProductRepoRegistry : IProductRepoRegistry
{
    private readonly List<ProductRepoRecord> _repos = [];
    private readonly object _gate = new();

    public InMemoryProductRepoRegistry()
    {
        var now = DateTime.UtcNow;
        _repos.AddRange(
        [
            new ProductRepoRecord
            {
                Id = Guid.NewGuid(),
                ProductSlug = "muuqwear",
                RepoKind = ProductRepoKinds.WebApp,
                Title = "MuuqWear Web",
                GithubRepoUrl = "https://github.com/kobneti/muuqwear-web",
                CreatedAt = now,
                UpdatedAt = now
            },
            new ProductRepoRecord
            {
                Id = Guid.NewGuid(),
                ProductSlug = "muuqwear",
                RepoKind = ProductRepoKinds.Api,
                Title = "MuuqWear API",
                GithubRepoUrl = "https://github.com/kobneti/muuqwear-api",
                CreatedAt = now,
                UpdatedAt = now
            }
        ]);
    }

    public Task<IReadOnlyList<ProductRepoRecord>> ListByProductSlugAsync(string productSlug, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<ProductRepoRecord>>(
                _repos
                    .Where(r => r.ProductSlug.Equals(productSlug, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(r => r.RepoKind)
                    .Select(Clone)
                    .ToList());
        }
    }

    public Task<ProductRepoRecord?> UpsertAsync(
        string productSlug,
        string repoKind,
        string title,
        string githubRepoUrl,
        CancellationToken ct = default)
    {
        var kind = ProductRepoKinds.Normalize(repoKind);
        var slug = productSlug.Trim();
        var now = DateTime.UtcNow;

        lock (_gate)
        {
            var hit = _repos.FirstOrDefault(r =>
                r.ProductSlug.Equals(slug, StringComparison.OrdinalIgnoreCase) &&
                r.RepoKind.Equals(kind, StringComparison.OrdinalIgnoreCase));

            if (hit is null)
            {
                hit = new ProductRepoRecord
                {
                    Id = Guid.NewGuid(),
                    ProductSlug = slug,
                    RepoKind = kind,
                    CreatedAt = now
                };
                _repos.Add(hit);
            }

            hit.Title = title.Trim();
            hit.GithubRepoUrl = githubRepoUrl.Trim();
            hit.UpdatedAt = now;
            return Task.FromResult<ProductRepoRecord?>(Clone(hit));
        }
    }

    public Task<bool> DeleteAsync(string productSlug, string repoKind, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var idx = _repos.FindIndex(r =>
                r.ProductSlug.Equals(productSlug, StringComparison.OrdinalIgnoreCase) &&
                r.RepoKind.Equals(ProductRepoKinds.Normalize(repoKind), StringComparison.OrdinalIgnoreCase));
            if (idx < 0)
                return Task.FromResult(false);
            _repos.RemoveAt(idx);
            return Task.FromResult(true);
        }
    }

    private static ProductRepoRecord Clone(ProductRepoRecord r) => new()
    {
        Id = r.Id,
        ProductSlug = r.ProductSlug,
        RepoKind = r.RepoKind,
        Title = r.Title,
        GithubRepoUrl = r.GithubRepoUrl,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt
    };
}
