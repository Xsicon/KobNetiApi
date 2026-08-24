using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using KobNeti.Api.Data;
using KobNeti.Api.DTOs;
using KobNeti.Api.Products;
using KobNeti.Api.Shared;

namespace KobNeti.Api.Services;

/// <summary>
/// W4.9–W4.10: read-only GitHub fetches into cache. Never POSTs/PATCHes/DELETEs to GitHub.
/// </summary>
public interface IGithubReadService
{
    Task<Response<GithubCacheDTO>> GetCachedAsync(string tenantId);
    Task<Response<GithubCacheDTO>> RefreshAsync(string tenantId, CancellationToken ct = default);
}

public class GithubReadService : IGithubReadService
{
    private static readonly Regex RepoUrlRegex = new(
        @"^https?://(?:www\.)?github\.com/(?<owner>[^/\s]+)/(?<repo>[^/\s#?]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ISupportStore _store;
    private readonly IProductRegistry _products;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GithubReadService> _logger;

    public GithubReadService(
        ISupportStore store,
        IProductRegistry products,
        IHttpClientFactory httpClientFactory,
        ILogger<GithubReadService> logger)
    {
        _store = store;
        _products = products;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<Response<GithubCacheDTO>> GetCachedAsync(string tenantId)
    {
        var product = await _products.GetBySlugAsync(tenantId);
        var pullsCache = await _store.GetGithubCacheAsync(tenantId, "pulls");
        var commitsCache = await _store.GetGithubCacheAsync(tenantId, "commits");
        return Response<GithubCacheDTO>.SuccessResponse(BuildDto(product?.GithubRepoUrl, pullsCache, commitsCache), "GitHub cache");
    }

    public async Task<Response<GithubCacheDTO>> RefreshAsync(string tenantId, CancellationToken ct = default)
    {
        var product = await _products.GetBySlugAsync(tenantId);
        if (product is null)
            return Response<GithubCacheDTO>.Fail("Product not found");
        if (string.IsNullOrWhiteSpace(product.GithubRepoUrl))
            return Response<GithubCacheDTO>.Fail("Set github_repo_url on the product first");

        if (!TryParseRepo(product.GithubRepoUrl, out var owner, out var repo))
            return Response<GithubCacheDTO>.Fail("Invalid GitHub repo URL (expected https://github.com/owner/repo)");

        var client = _httpClientFactory.CreateClient("github-readonly");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("KobNetiOps/1.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        // W4.10: GET only — never write to GitHub
        var pullsUrl = $"https://api.github.com/repos/{owner}/{repo}/pulls?state=all&per_page=20";
        var commitsUrl = $"https://api.github.com/repos/{owner}/{repo}/commits?per_page=20";

        string pullsJson;
        string commitsJson;
        try
        {
            using var pullsRes = await client.GetAsync(pullsUrl, ct);
            pullsJson = await pullsRes.Content.ReadAsStringAsync(ct);
            if (!pullsRes.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub pulls GET failed {Status}: {Body}", (int)pullsRes.StatusCode, pullsJson);
                return Response<GithubCacheDTO>.Fail($"GitHub pulls read failed ({(int)pullsRes.StatusCode})");
            }

            using var commitsRes = await client.GetAsync(commitsUrl, ct);
            commitsJson = await commitsRes.Content.ReadAsStringAsync(ct);
            if (!commitsRes.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub commits GET failed {Status}: {Body}", (int)commitsRes.StatusCode, commitsJson);
                return Response<GithubCacheDTO>.Fail($"GitHub commits read failed ({(int)commitsRes.StatusCode})");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GitHub refresh failed for {Tenant}", tenantId);
            return Response<GithubCacheDTO>.Fail("GitHub refresh failed");
        }

        var now = DateTime.UtcNow;
        var pullsCache = new GithubCacheEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RepoUrl = product.GithubRepoUrl,
            CacheKind = "pulls",
            PayloadJson = NormalizePullsJson(pullsJson),
            FetchedAt = now
        };
        var commitsCache = new GithubCacheEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RepoUrl = product.GithubRepoUrl,
            CacheKind = "commits",
            PayloadJson = NormalizeCommitsJson(commitsJson),
            FetchedAt = now
        };
        await _store.UpsertGithubCacheAsync(pullsCache);
        await _store.UpsertGithubCacheAsync(commitsCache);

        var dto = BuildDto(product.GithubRepoUrl, pullsCache, commitsCache);
        dto.Message = "Refreshed from GitHub (read-only)";
        return Response<GithubCacheDTO>.SuccessResponse(dto, "GitHub cache refreshed");
    }

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

    private static GithubCacheDTO BuildDto(string? repoUrl, GithubCacheEntity? pulls, GithubCacheEntity? commits) =>
        new()
        {
            RepoUrl = repoUrl ?? pulls?.RepoUrl ?? commits?.RepoUrl,
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
                state = item.TryGetProperty("state", out var s) ? s.GetString() : "",
                html_url = item.TryGetProperty("html_url", out var u) ? u.GetString() : "",
                author = item.TryGetProperty("user", out var user) && user.TryGetProperty("login", out var login)
                    ? login.GetString()
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
