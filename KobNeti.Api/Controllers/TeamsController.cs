using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;
using KobNeti.Api.Teams;

namespace KobNeti.Api.Controllers;

[Route("api/Teams")]
[Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
public class TeamsController : ApiControllerBase
{
    private readonly ITeamDirectory _teams;

    public TeamsController(ITeamDirectory teams) => _teams = teams;

    [HttpGet]
    public async Task<ActionResult<Response<List<TeamDTO>>>> List(CancellationToken ct)
    {
        var list = await _teams.ListAsync(ct);
        return Ok(Response<List<TeamDTO>>.SuccessResponse(list.Select(ToDto).ToList(), "Teams loaded"));
    }

    [HttpPost]
    public async Task<ActionResult<Response<TeamDTO>>> Create([FromBody] CreateTeamDTO dto, CancellationToken ct)
    {
        try
        {
            var created = await _teams.CreateAsync(dto.Name, dto.Slug, dto.Description, dto.ProductSlug, ct);
            return Ok(Response<TeamDTO>.SuccessResponse(ToDto(created), "Team created"));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(Response<TeamDTO>.Fail(ex.Message));
        }
    }

    [HttpPost("{id:guid}/members")]
    public async Task<ActionResult<Response<TeamDTO>>> AddMember(
        Guid id,
        [FromBody] AddTeamMemberDTO dto,
        CancellationToken ct)
    {
        try
        {
            var updated = await _teams.AddMemberAsync(id, dto.StaffId, dto.MemberRole, ct);
            if (updated is null)
                return NotFound(Response<TeamDTO>.Fail("Team not found"));
            return Ok(Response<TeamDTO>.SuccessResponse(ToDto(updated), "Member added"));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(Response<TeamDTO>.Fail(ex.Message));
        }
    }

    [HttpDelete("{id:guid}/members/{staffId:guid}")]
    public async Task<ActionResult<Response<object>>> RemoveMember(Guid id, Guid staffId, CancellationToken ct)
    {
        var ok = await _teams.RemoveMemberAsync(id, staffId, ct);
        if (!ok)
            return NotFound(Response<object>.Fail("Team or member not found"));
        return Ok(Response<object>.SuccessResponse(new { }, "Member removed"));
    }

    private static TeamDTO ToDto(TeamRecord t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Slug = t.Slug,
        Description = t.Description,
        ProductSlug = t.ProductSlug,
        Active = t.Active,
        Members = t.Members.Select(m => new TeamMemberDTO
        {
            StaffId = m.StaffId,
            Email = m.Email,
            DisplayName = m.DisplayName,
            MemberRole = m.MemberRole
        }).ToList()
    };
}
