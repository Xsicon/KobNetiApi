using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Services;
using KobNeti.Api.Shared;
using KobNeti.Api.Staff;
using KobNeti.Api.Tenancy;

namespace KobNeti.Api.Controllers;

[Route("api/Staff")]
[Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
public class StaffController : ApiControllerBase
{
    private readonly IStaffDirectory _staff;
    private readonly IAuditService _audit;
    private readonly ITenantContextAccessor _tenant;

    public StaffController(IStaffDirectory staff, IAuditService audit, ITenantContextAccessor tenant)
    {
        _staff = staff;
        _audit = audit;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<Response<List<StaffMemberDTO>>>> List(CancellationToken ct)
    {
        var list = await _staff.ListAsync(ct);
        return Ok(Response<List<StaffMemberDTO>>.SuccessResponse(
            list.Select(ToDto).ToList(),
            "Staff loaded"));
    }

    [HttpPost("invite")]
    public async Task<ActionResult<Response<StaffMemberDTO>>> Invite([FromBody] InviteStaffDTO dto, CancellationToken ct)
    {
        try
        {
            var created = await _staff.InviteAsync(
                dto.Email,
                dto.DisplayName,
                dto.Role,
                dto.ProductSlugs ?? [],
                ct);
            await _audit.WriteAsync(
                _tenant.Current?.TenantId ?? "ops",
                AuditActions.StaffInvite, "staff", created.Id.ToString(),
                AdminRoleClaims.GetUserId(User), AdminRoleClaims.GetDisplayName(User),
                null, new { created.Email, created.Role });
            return Ok(Response<StaffMemberDTO>.SuccessResponse(ToDto(created), "Staff invited"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(Response<StaffMemberDTO>.Fail(ex.Message));
        }
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<Response<StaffMemberDTO>>> Deactivate(Guid id, CancellationToken ct)
    {
        var updated = await _staff.SetActiveAsync(id, false, ct);
        if (updated is null)
            return NotFound(Response<StaffMemberDTO>.Fail("Staff not found"));
        await _audit.WriteAsync(_tenant.Current?.TenantId ?? "ops", AuditActions.StaffActivate, "staff", id.ToString(),
            AdminRoleClaims.GetUserId(User), AdminRoleClaims.GetDisplayName(User),
            new { active = true }, new { active = false });
        return Ok(Response<StaffMemberDTO>.SuccessResponse(ToDto(updated), "Staff deactivated"));
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<Response<StaffMemberDTO>>> Activate(Guid id, CancellationToken ct)
    {
        var updated = await _staff.SetActiveAsync(id, true, ct);
        if (updated is null)
            return NotFound(Response<StaffMemberDTO>.Fail("Staff not found"));
        await _audit.WriteAsync(_tenant.Current?.TenantId ?? "ops", AuditActions.StaffActivate, "staff", id.ToString(),
            AdminRoleClaims.GetUserId(User), AdminRoleClaims.GetDisplayName(User),
            new { active = false }, new { active = true });
        return Ok(Response<StaffMemberDTO>.SuccessResponse(ToDto(updated), "Staff activated"));
    }

    [HttpPut("{id:guid}/products")]
    public async Task<ActionResult<Response<StaffMemberDTO>>> SetProducts(
        Guid id,
        [FromBody] UpdateStaffProductsDTO dto,
        CancellationToken ct)
    {
        var updated = await _staff.SetProductAccessAsync(id, dto.ProductSlugs ?? [], ct);
        if (updated is null)
            return NotFound(Response<StaffMemberDTO>.Fail("Staff not found"));
        await _audit.WriteAsync(_tenant.Current?.TenantId ?? "ops", AuditActions.StaffProducts, "staff", id.ToString(),
            AdminRoleClaims.GetUserId(User), AdminRoleClaims.GetDisplayName(User),
            null, new { productSlugs = dto.ProductSlugs });
        return Ok(Response<StaffMemberDTO>.SuccessResponse(ToDto(updated), "Product access updated"));
    }

    private static StaffMemberDTO ToDto(StaffAccessRecord s) => new()
    {
        Id = s.Id,
        Email = s.Email,
        DisplayName = s.DisplayName,
        Role = s.Role,
        Active = s.Active,
        ProductSlugs = s.ProductSlugs.ToList()
    };
}
