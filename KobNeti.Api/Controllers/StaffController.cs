using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;
using KobNeti.Api.Staff;

namespace KobNeti.Api.Controllers;

[Route("api/Staff")]
[Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
public class StaffController : ApiControllerBase
{
    private readonly IStaffDirectory _staff;

    public StaffController(IStaffDirectory staff) => _staff = staff;

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
        return Ok(Response<StaffMemberDTO>.SuccessResponse(ToDto(updated), "Staff deactivated"));
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<Response<StaffMemberDTO>>> Activate(Guid id, CancellationToken ct)
    {
        var updated = await _staff.SetActiveAsync(id, true, ct);
        if (updated is null)
            return NotFound(Response<StaffMemberDTO>.Fail("Staff not found"));
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
