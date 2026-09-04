using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
public class StaffController : ApiControllerBase
{
    private readonly IStaffDirectory _staff;
    private readonly InMemoryStaffDirectory _demoStaff;
    private readonly IAuditService _audit;
    private readonly ITenantContextAccessor _tenant;

    public StaffController(
        IStaffDirectory staff,
        InMemoryStaffDirectory demoStaff,
        IAuditService audit,
        ITenantContextAccessor tenant)
    {
        _staff = staff;
        _demoStaff = demoStaff;
        _audit = audit;
        _tenant = tenant;
    }

    /// <summary>Staff picker for support agents (assign tasks, escalate chat, etc.).</summary>
    [HttpGet("assignable")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<List<StaffMemberDTO>>>> ListAssignable(CancellationToken ct)
    {
        var tenantId = _tenant.Current?.TenantId;
        var list = await _staff.ListAsync(ct);
        var filtered = FilterAssignable(list, tenantId);

        if (filtered.Count == 0 && list.Count > 0)
            filtered = ToActiveDtoList(list);

        if (filtered.Count == 0)
        {
            var demo = await _demoStaff.ListAsync(ct);
            filtered = FilterAssignable(demo, tenantId);
            if (filtered.Count == 0 && demo.Count > 0)
                filtered = ToActiveDtoList(demo);
        }

        EnsureCurrentUserIncluded(filtered);
        return Ok(Response<List<StaffMemberDTO>>.SuccessResponse(filtered, "Assignable staff loaded"));
    }

    private void EnsureCurrentUserIncluded(List<StaffMemberDTO> list)
    {
        var email = User.FindFirstValue(ClaimTypes.Email)
                    ?? User.FindFirstValue(JwtRegisteredClaimNames.Email);
        if (string.IsNullOrWhiteSpace(email))
            return;

        if (list.Any(s => string.Equals(s.Email, email, StringComparison.OrdinalIgnoreCase)))
            return;

        var displayName = AdminRoleClaims.GetDisplayName(User);
        if (displayName.Contains('@', StringComparison.Ordinal))
        {
            var at = displayName.IndexOf('@');
            displayName = at > 0 ? displayName[..at] : displayName;
        }

        list.Add(new StaffMemberDTO
        {
            Id = AdminRoleClaims.GetUserId(User) ?? Guid.NewGuid(),
            Email = email,
            DisplayName = displayName,
            Role = User.FindFirstValue(AdminRoleClaims.RoleClaimType) ?? StaffRoles.Support,
            Active = true,
            ProductSlugs = AdminRoleClaims.GetProductSlugs(User)
                .Where(p => !string.Equals(p, AdminRoleClaims.AllProducts, StringComparison.Ordinal))
                .ToList()
        });

        list.Sort((a, b) => string.Compare(
            a.DisplayName ?? a.Email,
            b.DisplayName ?? b.Email,
            StringComparison.OrdinalIgnoreCase));
    }

    private static List<StaffMemberDTO> ToActiveDtoList(IReadOnlyList<StaffAccessRecord> list) =>
        list
            .Where(s => s.Active)
            .Select(ToDto)
            .OrderBy(s => s.DisplayName ?? s.Email, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<StaffMemberDTO> FilterAssignable(IReadOnlyList<StaffAccessRecord> list, string? tenantId) =>
        list
            .Where(s => s.Active)
            .Where(s => string.IsNullOrWhiteSpace(tenantId)
                        || s.ProductSlugs.Count == 0
                        || s.ProductSlugs.Any(p => string.Equals(p, tenantId, StringComparison.OrdinalIgnoreCase)))
            .Select(ToDto)
            .OrderBy(s => s.DisplayName ?? s.Email, StringComparer.OrdinalIgnoreCase)
            .ToList();

    [HttpGet]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<List<StaffMemberDTO>>>> List(CancellationToken ct)
    {
        var list = await _staff.ListAsync(ct);
        return Ok(Response<List<StaffMemberDTO>>.SuccessResponse(
            list.Select(ToDto).ToList(),
            "Staff loaded"));
    }

    [HttpPost("invite")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
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
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
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
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
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
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
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
