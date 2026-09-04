using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Products;
using KobNeti.Api.Services;
using KobNeti.Api.Shared;
using KobNeti.Api.Tenancy;

namespace KobNeti.Api.Controllers;

[Route("api/products")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
public class ProductsController : ApiControllerBase
{
    private readonly IProductRegistry _registry;
    private readonly IProductRepoRegistry _repos;
    private readonly ITenantResolver _tenants;
    private readonly UpstreamApiClient _upstream;
    private readonly IConfiguration _config;

    public ProductsController(
        IProductRegistry registry,
        IProductRepoRegistry repos,
        ITenantResolver tenants,
        UpstreamApiClient upstream,
        IConfiguration config)
    {
        _registry = registry;
        _repos = repos;
        _tenants = tenants;
        _upstream = upstream;
        _config = config;
    }

    /// <summary>Product Registry list for the ops Hub switcher (Module 3).</summary>
    [HttpGet]
    public async Task<ActionResult<Response<List<ProductDTO>>>> List(CancellationToken ct)
    {
        var products = await _registry.ListEnabledAsync(ct);
        var list = new List<ProductDTO>();
        foreach (var p in products.Where(p => AdminRoleClaims.CanAccessProduct(User, p.Slug)))
        {
            var dto = await ToDtoAsync(p, ct);
            list.Add(dto);
        }

        return Ok(Response<List<ProductDTO>>.SuccessResponse(list, "Products loaded"));
    }

    /// <summary>Rotate embed/public key for a product (W1.13). Platform admin only.</summary>
    [HttpPost("{slug}/rotate-key")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<RotateEmbedKeyDTO>>> RotateKey(string slug, CancellationToken ct)
    {
        var updated = await _registry.RotatePublicKeyAsync(slug, ct);
        if (updated is null)
            return NotFound(Response<RotateEmbedKeyDTO>.Fail("Product not found"));

        if (_tenants is ProductTenantResolver resolver)
            resolver.InvalidateCache();

        var apiBase = _config["Support:PublicApiBaseUrl"]
                      ?? $"{Request.Scheme}://{Request.Host}";

        return Ok(Response<RotateEmbedKeyDTO>.SuccessResponse(new RotateEmbedKeyDTO
        {
            PublicKey = updated.PublicKey,
            WidgetSnippet = EmbedKeyHelper.BuildWidgetSnippet(updated.PublicKey, apiBase)
        }, "Embed key rotated"));
    }

    [HttpGet("{slug}/widget-snippet")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<RotateEmbedKeyDTO>>> WidgetSnippet(string slug, CancellationToken ct)
    {
        var product = await _registry.GetBySlugAsync(slug, ct);
        if (product is null)
            return NotFound(Response<RotateEmbedKeyDTO>.Fail("Product not found"));

        var apiBase = _config["Support:PublicApiBaseUrl"]
                      ?? $"{Request.Scheme}://{Request.Host}";

        return Ok(Response<RotateEmbedKeyDTO>.SuccessResponse(new RotateEmbedKeyDTO
        {
            PublicKey = product.PublicKey,
            WidgetSnippet = EmbedKeyHelper.BuildWidgetSnippet(product.PublicKey, apiBase)
        }, "Widget snippet"));
    }

    [HttpPatch("{slug}/github-repo")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<ProductDTO>>> UpdateGithubRepo(
        string slug, [FromBody] UpdateProductGithubRepoDTO request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.GithubRepoUrl) &&
            !GithubReadService.TryParseRepo(request.GithubRepoUrl, out _, out _))
        {
            return BadRequest(Response<ProductDTO>.Fail("Invalid GitHub repo URL"));
        }

        var updated = await _registry.UpdateGithubRepoUrlAsync(slug, request.GithubRepoUrl, ct);
        if (updated is null)
            return NotFound(Response<ProductDTO>.Fail("Product not found"));

        if (!string.IsNullOrWhiteSpace(request.GithubRepoUrl))
        {
            await _repos.UpsertAsync(
                slug,
                ProductRepoKinds.WebApp,
                ProductRepoKinds.DefaultTitle(updated.DisplayName, ProductRepoKinds.WebApp),
                request.GithubRepoUrl,
                ct);
        }

        return Ok(Response<ProductDTO>.SuccessResponse(await ToDtoAsync(updated, ct), "GitHub repo updated"));
    }

    /// <summary>Linked GitHub repos for a product (web app, API, etc.).</summary>
    [HttpGet("{slug}/repos")]
    public async Task<ActionResult<Response<List<ProductRepoDTO>>>> ListRepos(string slug, CancellationToken ct)
    {
        if (!AdminRoleClaims.CanAccessProduct(User, slug))
            return StatusCode(403, Response<List<ProductRepoDTO>>.Fail($"No access to product '{slug}'."));

        var product = await _registry.GetBySlugAsync(slug, ct);
        if (product is null)
            return NotFound(Response<List<ProductRepoDTO>>.Fail("Product not found"));

        var rows = await _repos.ListByProductSlugAsync(slug, ct);
        if (rows.Count == 0 && !string.IsNullOrWhiteSpace(product.GithubRepoUrl))
        {
            rows =
            [
                new ProductRepoRecord
                {
                    ProductSlug = slug,
                    RepoKind = ProductRepoKinds.WebApp,
                    Title = ProductRepoKinds.DefaultTitle(product.DisplayName, ProductRepoKinds.WebApp),
                    GithubRepoUrl = product.GithubRepoUrl
                }
            ];
        }

        return Ok(Response<List<ProductRepoDTO>>.SuccessResponse(
            rows.Select(r => new ProductRepoDTO
            {
                Id = r.Id,
                RepoKind = r.RepoKind,
                Title = r.Title,
                GithubRepoUrl = r.GithubRepoUrl
            }).ToList(),
            "Linked repos loaded"));
    }

    [HttpPut("{slug}/repos/{repoKind}")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<ProductRepoDTO>>> UpsertRepo(
        string slug,
        string repoKind,
        [FromBody] UpsertProductRepoDTO request,
        CancellationToken ct)
    {
        if (!ProductRepoKinds.IsValid(repoKind))
            return BadRequest(Response<ProductRepoDTO>.Fail("repoKind must be web_app, api, mobile, or internal"));

        if (!GithubReadService.TryParseRepo(request.GithubRepoUrl, out _, out _))
            return BadRequest(Response<ProductRepoDTO>.Fail("Invalid GitHub repo URL"));

        var product = await _registry.GetBySlugAsync(slug, ct);
        if (product is null)
            return NotFound(Response<ProductRepoDTO>.Fail("Product not found"));

        var kind = ProductRepoKinds.Normalize(repoKind);
        var title = string.IsNullOrWhiteSpace(request.Title)
            ? ProductRepoKinds.DefaultTitle(product.DisplayName, kind)
            : request.Title.Trim();

        var saved = await _repos.UpsertAsync(slug, kind, title, request.GithubRepoUrl.Trim(), ct);
        if (saved is null)
            return BadRequest(Response<ProductRepoDTO>.Fail("Failed to save linked repo"));

        if (kind == ProductRepoKinds.WebApp)
            await _registry.UpdateGithubRepoUrlAsync(slug, saved.GithubRepoUrl, ct);

        return Ok(Response<ProductRepoDTO>.SuccessResponse(new ProductRepoDTO
        {
            Id = saved.Id,
            RepoKind = saved.RepoKind,
            Title = saved.Title,
            GithubRepoUrl = saved.GithubRepoUrl
        }, "Linked repo saved"));
    }

    [HttpDelete("{slug}/repos/{repoKind}")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<object>>> DeleteRepo(string slug, string repoKind, CancellationToken ct)
    {
        if (!ProductRepoKinds.IsValid(repoKind))
            return BadRequest(Response<object>.Fail("repoKind must be web_app, api, mobile, or internal"));

        var deleted = await _repos.DeleteAsync(slug, repoKind, ct);
        if (!deleted)
            return NotFound(Response<object>.Fail("Linked repo not found"));

        return Ok(Response<object>.SuccessResponse(new { }, "Linked repo removed"));
    }

    [HttpPatch("{slug}/upstream-api")]
    public async Task<ActionResult<Response<ProductDTO>>> UpdateUpstreamApi(
        string slug, [FromBody] UpdateProductUpstreamApiDTO request, CancellationToken ct)
    {
        if (!AdminRoleClaims.CanAccessProduct(User, slug))
            return StatusCode(403, Response<ProductDTO>.Fail($"No access to product '{slug}'."));

        var url = request.UpstreamApiBaseUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(url))
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return BadRequest(Response<ProductDTO>.Fail("Enter a valid absolute URL (e.g. http://localhost:5243/)."));

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return BadRequest(Response<ProductDTO>.Fail("Product API URL must use http or https."));
        }

        var updated = await _registry.UpdateUpstreamApiBaseUrlAsync(slug, url, ct);
        if (updated is null)
            return NotFound(Response<ProductDTO>.Fail("Product not found"));

        if (_tenants is ProductTenantResolver resolver)
            resolver.InvalidateCache();

        return Ok(Response<ProductDTO>.SuccessResponse(await ToDtoAsync(updated, ct), "Product API URL saved"));
    }

    [HttpGet("{slug}/upstream-status")]
    public async Task<ActionResult<Response<ProductUpstreamStatusDTO>>> UpstreamStatus(string slug, CancellationToken ct)
    {
        if (!AdminRoleClaims.CanAccessProduct(User, slug))
            return StatusCode(403, Response<ProductUpstreamStatusDTO>.Fail($"No access to product '{slug}'."));

        var product = await _registry.GetBySlugAsync(slug, ct);
        if (product is null)
            return NotFound(Response<ProductUpstreamStatusDTO>.Fail("Product not found"));

        var status = await _upstream.ProbeAsync(slug);
        if (string.IsNullOrWhiteSpace(status.UpstreamApiBaseUrl))
            status.UpstreamApiBaseUrl = product.UpstreamApiBaseUrl;

        return Ok(Response<ProductUpstreamStatusDTO>.SuccessResponse(status, "Status loaded"));
    }

    private async Task<ProductDTO> ToDtoAsync(ProductRecord p, CancellationToken ct)
    {
        var linked = await _repos.ListByProductSlugAsync(p.Slug, ct);
        var dto = new ProductDTO
        {
            Id = p.Id,
            TenantId = p.Slug,
            DisplayName = p.DisplayName,
            ProductType = p.ProductType,
            Status = p.Status,
            SupportTier = p.SupportTier,
            PublicKey = p.PublicKey,
            PublicHelpCenterUrl = p.PublicHelpCenterUrl ?? "",
            UpstreamApiBaseUrl = p.UpstreamApiBaseUrl,
            GithubRepoUrl = p.GithubRepoUrl,
            Enabled = p.Enabled,
            LinkedRepos = linked.Select(r => new ProductRepoDTO
            {
                Id = r.Id,
                RepoKind = r.RepoKind,
                Title = r.Title,
                GithubRepoUrl = r.GithubRepoUrl
            }).ToList()
        };

        if (dto.LinkedRepos.Count == 0 && !string.IsNullOrWhiteSpace(p.GithubRepoUrl))
        {
            dto.LinkedRepos.Add(new ProductRepoDTO
            {
                RepoKind = ProductRepoKinds.WebApp,
                Title = ProductRepoKinds.DefaultTitle(p.DisplayName, ProductRepoKinds.WebApp),
                GithubRepoUrl = p.GithubRepoUrl
            });
        }

        return dto;
    }
}
