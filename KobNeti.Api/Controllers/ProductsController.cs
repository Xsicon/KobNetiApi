using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Products;
using KobNeti.Api.Shared;

namespace KobNeti.Api.Controllers;

[Route("api/products")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
public class ProductsController : ApiControllerBase
{
    private readonly IProductRegistry _registry;

    public ProductsController(IProductRegistry registry) => _registry = registry;

    /// <summary>Product Registry list for the ops Hub switcher (Module 3).</summary>
    [HttpGet]
    public async Task<ActionResult<Response<List<ProductDTO>>>> List(CancellationToken ct)
    {
        var products = await _registry.ListEnabledAsync(ct);
        var list = products
            .Where(p => AdminRoleClaims.CanAccessProduct(User, p.Slug))
            .Select(p => new ProductDTO
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
            }).ToList();

        return Ok(Response<List<ProductDTO>>.SuccessResponse(list, "Products loaded"));
    }
}
