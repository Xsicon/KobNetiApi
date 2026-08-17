using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Products;
using KobNeti.Api.Shared;
using KobNeti.Api.Tenancy;

namespace KobNeti.Api.Controllers;

[Route("api/products")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
public class ProductsController : ApiControllerBase
{
    private readonly IProductRegistry _registry;
    private readonly ITenantResolver _tenants;
    private readonly IConfiguration _config;

    public ProductsController(IProductRegistry registry, ITenantResolver tenants, IConfiguration config)
    {
        _registry = registry;
        _tenants = tenants;
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
        Enabled = p.Enabled
    };
}
