using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using KobNeti.Api.Data;
using KobNeti.Api.DTOs;
using KobNeti.Api.Products;
using KobNeti.Api.Shared;
using KobNeti.Api.Staff;

namespace KobNeti.Api.Services;

/// <summary>
/// W4.9–W4.10: read-only GitHub fetches into cache. Never POSTs/PATCHes/DELETEs to GitHub.
/// Products can link multiple repos (web app + API) per tenant.
/// </summary>
public interface IGithubReadService
{
    Task<Response<List<ProductRepoDTO>>> ListReposAsync(string tenantId);
    Task<Response<GithubCacheDTO>> GetCachedAsync(string tenantId, string? repoKey = null);
    Task<Response<GithubCacheDTO>> RefreshAsync(string tenantId, string? repoKey = null, CancellationToken ct = default);
}

public class GithubReadService : IGithubReadService
{
    private static readonly Regex RepoUrlRegex = new(
        @"^https?://(?:www\.)?github\.com/(?<owner>[^/\s]+)/(?<repo>[^/\s#?]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ISupportStore _store;
    private readonly IProductRegistry _products;
    private readonly IProductRepoRegistry _repos;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GithubReadService> _logger;
    private readonly IStaffDirectory _staff;

    public GithubReadService(
        ISupportStore store,
        IProductRegistry products,
        IProductRepoRegistry repos,
        IHttpClientFactory httpClientFactory,
        ILogger<GithubReadService> logger,
        IStaffDirectory staff)
    {
        _store = store;
        _products = products;
        _repos = repos;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _staff = staff;
    }

    public async Task<Response<List<ProductRepoDTO>>> ListReposAsync(string tenantId)
    {
        var resolved = await ResolveReposAsync(tenantId);
        return Response<List<ProductRepoDTO>>.SuccessResponse(
            resolved.Select(ToRepoDto).ToList(),
            "Linked repos loaded");
    }

    public async Task<Response<GithubCacheDTO>> GetCachedAsync(string tenantId, string? repoKey = null)
    {
        await EngineeringSampleData.EnsureSeededAsync(_store, tenantId, _staff);
        var resolved = await ResolveReposAsync(tenantId);
        if (resolved.Count == 0)
            return Response<GithubCacheDTO>.SuccessResponse(new GithubCacheDTO(), "No linked repos");

        var target = PickRepo(resolved, repoKey);
        var pullsCache = await _store.GetGithubCacheAsync(tenantId, target.RepoKind, "pulls");
        var commitsCache = await _store.GetGithubCacheAsync(tenantId, target.RepoKind, "commits");
        var dto = BuildDto(target, pullsCache, commitsCache);
        dto.LinkedRepos = resolved.Select(ToRepoDto).ToList();
        return Response<GithubCacheDTO>.SuccessResponse(dto, "GitHub cache");
    }

    public async Task<Response<GithubCacheDTO>> RefreshAsync(string tenantId, string? repoKey = null, CancellationToken ct = default)
    {
        var resolved = await ResolveReposAsync(tenantId);
        if (resolved.Count == 0)
            return Response<GithubCacheDTO>.Fail("Link at least one GitHub repo for this product (web app and/or API).");

        if (string.IsNullOrWhiteSpace(repoKey))
        {
            GithubCacheDTO? last = null;
            foreach (var repo in resolved)
            {
                var result = await RefreshOneAsync(tenantId, repo, ct);
                if (!result.Success)
                    return result;
                last = result.Data;
            }

            last ??= new GithubCacheDTO();
            last.LinkedRepos = resolved.Select(ToRepoDto).ToList();
            last.Message = $"Refreshed {resolved.Count} linked repo(s) from GitHub (read-only)";
            return Response<GithubCacheDTO>.SuccessResponse(last, "GitHub cache refreshed");
        }

        var hit = PickRepo(resolved, repoKey);
        var single = await RefreshOneAsync(tenantId, hit, ct);
        if (single.Success && single.Data is not null)
        {
            single.Data.LinkedRepos = resolved.Select(ToRepoDto).ToList();
        }
        return single;
    }

    private async Task<Response<GithubCacheDTO>> RefreshOneAsync(
        string tenantId,
        ProductRepoRecord repo,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(repo.GithubRepoUrl))
            return Response<GithubCacheDTO>.Fail($"Set a GitHub URL for {repo.Title} first.");

        if (!TryParseRepo(repo.GithubRepoUrl, out var owner, out var ghRepo))
            return Response<GithubCacheDTO>.Fail($"Invalid GitHub URL for {repo.Title} (expected https://github.com/owner/repo)");

        var client = _httpClientFactory.CreateClient("github-readonly");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("KobNetiOps/1.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        var pullsUrl = $"https://api.github.com/repos/{owner}/{ghRepo}/pulls?state=all&per_page=20";
        var commitsUrl = $"https://api.github.com/repos/{owner}/{ghRepo}/commits?per_page=20";

        string pullsJson;
        string commitsJson;
        try
        {
            using var pullsRes = await client.GetAsync(pullsUrl, ct);
            pullsJson = await pullsRes.Content.ReadAsStringAsync(ct);
            if (!pullsRes.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub pulls GET failed {Status}: {Body}", (int)pullsRes.StatusCode, pullsJson);
                return Response<GithubCacheDTO>.Fail($"GitHub pulls read failed for {repo.Title} ({(int)pullsRes.StatusCode})");
            }

            using var commitsRes = await client.GetAsync(commitsUrl, ct);
            commitsJson = await commitsRes.Content.ReadAsStringAsync(ct);
            if (!commitsRes.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub commits GET failed {Status}: {Body}", (int)commitsRes.StatusCode, commitsJson);
                return Response<GithubCacheDTO>.Fail($"GitHub commits read failed for {repo.Title} ({(int)commitsRes.StatusCode})");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GitHub refresh failed for {Tenant}/{RepoKind}", tenantId, repo.RepoKind);
            return Response<GithubCacheDTO>.Fail($"GitHub refresh failed for {repo.Title}");
        }

        var now = DateTime.UtcNow;
        var pullsCache = new GithubCacheEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RepoKey = repo.RepoKind,
            RepoUrl = repo.GithubRepoUrl,
            CacheKind = "pulls",
            PayloadJson = NormalizePullsJson(pullsJson),
            FetchedAt = now
        };
        var commitsCache = new GithubCacheEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RepoKey = repo.RepoKind,
            RepoUrl = repo.GithubRepoUrl,
            CacheKind = "commits",
            PayloadJson = NormalizeCommitsJson(commitsJson),
            FetchedAt = now
        };
        await _store.UpsertGithubCacheAsync(pullsCache);
        await _store.UpsertGithubCacheAsync(commitsCache);

        var dto = BuildDto(repo, pullsCache, commitsCache);
        dto.Message = $"Refreshed {repo.Title} from GitHub (read-only)";
        return Response<GithubCacheDTO>.SuccessResponse(dto, "GitHub cache refreshed");
    }

    private async Task<IReadOnlyList<ProductRepoRecord>> ResolveReposAsync(string tenantId)
    {
        var linked = await _repos.ListByProductSlugAsync(tenantId);
        if (linked.Count > 0)
            return linked;

        var product = await _products.GetBySlugAsync(tenantId);
        if (product is null || string.IsNullOrWhiteSpace(product.GithubRepoUrl))
            return [];

        return
        [
            new ProductRepoRecord
            {
                Id = Guid.Empty,
                ProductSlug = tenantId,
                RepoKind = ProductRepoKinds.WebApp,
                Title = ProductRepoKinds.DefaultTitle(product.DisplayName, ProductRepoKinds.WebApp),
                GithubRepoUrl = product.GithubRepoUrl,
                UpdatedAt = product.UpdatedAt
            }
        ];
    }

    private static ProductRepoRecord PickRepo(IReadOnlyList<ProductRepoRecord> repos, string? repoKey)
    {
        if (!string.IsNullOrWhiteSpace(repoKey))
        {
            var normalized = ProductRepoKinds.Normalize(repoKey);
            return repos.First(r => r.RepoKind.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        }

        return repos.FirstOrDefault(r => r.RepoKind == ProductRepoKinds.WebApp) ?? repos[0];
    }

    private static ProductRepoDTO ToRepoDto(ProductRepoRecord r) => new()
    {
        Id = r.Id,
        RepoKind = r.RepoKind,
        Title = r.Title,
        GithubRepoUrl = r.GithubRepoUrl,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt
    };

    public static bool TryParseRepo(string url, out string owner, out string repo)
    {
        owner = "";
        repo = "";
        var m = RepoUrlRegex.Match(url.Trim());
        if (!m.Success)
            return false;
        owner = m.Groups["owner"].Value;
        repo = m.Groups["repo"].Value.TrimEnd('/');
        if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            repo = repo[..^4];
        return !string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(repo);
    }

    private static GithubCacheDTO BuildDto(
        ProductRepoRecord repo,
        GithubCacheEntity? pulls,
        GithubCacheEntity? commits) =>
        new()
        {
            RepoKey = repo.RepoKind,
            RepoTitle = repo.Title,
            RepoUrl = repo.GithubRepoUrl,
            PullsFetchedAt = pulls?.FetchedAt,
            CommitsFetchedAt = commits?.FetchedAt,
            Pulls = DeserializePulls(pulls?.PayloadJson),
            Commits = DeserializeCommits(commits?.PayloadJson)
        };

    private static string NormalizePullsJson(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        var list = new List<object>();
        foreach (var item in doc.RootElement.EnumerateArray().Take(20))
        {
            list.Add(new
            {
                number = item.TryGetProperty("number", out var n) ? n.GetInt32() : 0,
                title = item.TryGetProperty("title", out var t) ? t.GetString() : "",
                state = item.TryGetProperty("merged_at", out var merged) && merged.ValueKind is JsonValueKind.String
                    ? "merged"
                    : item.TryGetProperty("state", out var s) ? s.GetString() : "",
                html_url = item.TryGetProperty("html_url", out var u) ? u.GetString() : "",
                author = item.TryGetProperty("user", out var user) && user.TryGetProperty("login", out var login)
                    ? login.GetString()
                    : null,
                head_ref = item.TryGetProperty("head", out var head) && head.TryGetProperty("ref", out var href)
                    ? href.GetString()
                    : null,
                updated_at = item.TryGetProperty("updated_at", out var ua) ? ua.GetString() : null
            });
        }
        return JsonSerializer.Serialize(list);
    }

    private static string NormalizeCommitsJson(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        var list = new List<object>();
        foreach (var item in doc.RootElement.EnumerateArray().Take(20))
        {
            var sha = item.TryGetProperty("sha", out var shaEl) ? shaEl.GetString() ?? "" : "";
            var message = "";
            string? author = null;
            string? date = null;
            if (item.TryGetProperty("commit", out var commit))
            {
                if (commit.TryGetProperty("message", out var msg))
                    message = msg.GetString()?.Split('\n')[0] ?? "";
                if (commit.TryGetProperty("author", out var a))
                {
                    if (a.TryGetProperty("name", out var name))
                        author = name.GetString();
                    if (a.TryGetProperty("date", out var d))
                        date = d.GetString();
                }
            }
            var html = item.TryGetProperty("html_url", out var hu) ? hu.GetString() : $"https://github.com/commit/{sha}";
            list.Add(new { sha, message, html_url = html, author, date });
        }
        return JsonSerializer.Serialize(list);
    }

    private static List<GithubPullDTO> DeserializePulls(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            var list = new List<GithubPullDTO>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                list.Add(new GithubPullDTO
                {
                    Number = item.TryGetProperty("number", out var n) ? n.GetInt32() : 0,
                    Title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                    State = item.TryGetProperty("state", out var s) ? s.GetString() ?? "" : "",
                    HtmlUrl = item.TryGetProperty("html_url", out var u) ? u.GetString() ?? "" : "",
                    Author = item.TryGetProperty("author", out var a) ? a.GetString() : null,
                    HeadRef = item.TryGetProperty("head_ref", out var href) ? href.GetString() : null,
                    UpdatedAt = item.TryGetProperty("updated_at", out var ua) && DateTime.TryParse(ua.GetString(), out var dt)
                        ? dt.ToUniversalTime()
                        : null
                });
            }
            return list;
        }
        catch
        {
            return [];
        }
    }

    private static List<GithubCommitDTO> DeserializeCommits(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            var list = new List<GithubCommitDTO>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                list.Add(new GithubCommitDTO
                {
                    Sha = item.TryGetProperty("sha", out var sha) ? sha.GetString() ?? "" : "",
                    Message = item.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : "",
                    HtmlUrl = item.TryGetProperty("html_url", out var u) ? u.GetString() ?? "" : "",
                    Author = item.TryGetProperty("author", out var a) ? a.GetString() : null,
                    Date = item.TryGetProperty("date", out var d) && DateTime.TryParse(d.GetString(), out var dt)
                        ? dt.ToUniversalTime()
                        : null
                });
            }
            return list;
        }
        catch
        {
            return [];
        }
    }
}
