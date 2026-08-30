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
    private readonly ITenantResolver _tenants;
    private readonly UpstreamApiClient _upstream;
    private readonly IConfiguration _config;

    public ProductsController(
        IProductRegistry registry,
        ITenantResolver tenants,
        UpstreamApiClient upstream,
        IConfiguration config)
    {
        _registry = registry;
        _tenants = tenants;
        _upstream = upstream;
        _config = config;
    }

    /// <summary>Product Registry list for the ops Hub switcher (Module 3).</summary>
    [HttpGet]
    public async Task<ActionResult<Response<List<ProductDTO>>>> List(CancellationToken ct)
    {
        var products = await _registry.ListEnabledAsync(ct);
        var list = products
            .Where(p => AdminRoleClaims.CanAccessProduct(User, p.Slug))
            .Select(ToDto)
            .ToList();

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

        return Ok(Response<ProductDTO>.SuccessResponse(ToDto(updated), "GitHub repo updated"));
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

        return Ok(Response<ProductDTO>.SuccessResponse(ToDto(updated), "Product API URL saved"));
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

    private static ProductDTO ToDto(ProductRecord p) => new()
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
        Enabled = p.Enabled
    };
}
