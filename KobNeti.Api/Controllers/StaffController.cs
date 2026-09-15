using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Email;
using KobNeti.Api.Services;
using KobNeti.Api.Shared;
using KobNeti.Api.Staff;
using KobNeti.Api.Teams;
using KobNeti.Api.Tenancy;

namespace KobNeti.Api.Controllers;

[Route("api/Staff")]
public class StaffController : ApiControllerBase
{
    private readonly IStaffDirectory _staff;
    private readonly ITeamDirectory _teams;
    private readonly IStaffInviteEmailService _inviteEmail;
    private readonly IAuditService _audit;
    private readonly ITenantContextAccessor _tenant;

    public StaffController(
        IStaffDirectory staff,
        ITeamDirectory teams,
        IStaffInviteEmailService inviteEmail,
        IAuditService audit,
        ITenantContextAccessor tenant)
    {
        _staff = staff;
        _teams = teams;
        _inviteEmail = inviteEmail;
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

        EnsureCurrentUserIncluded(filtered);
        return Ok(Response<List<StaffMemberDTO>>.SuccessResponse(filtered, "Assignable staff loaded"));
    }

    [HttpGet]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<List<StaffMemberDTO>>>> List(CancellationToken ct)
    {
        var list = await _staff.ListAsync(ct);
        var dtos = list.Select(ToDto).ToList();
        EnsureCurrentUserIncluded(dtos);
        return Ok(Response<List<StaffMemberDTO>>.SuccessResponse(dtos, "Staff loaded"));
    }

    [HttpGet("stats")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<StaffStatsDTO>>> Stats(CancellationToken ct)
    {
        await CloseOwnPendingInviteAsync(ct);
        var staff = (await _staff.ListAsync(ct)).Select(ToDto).ToList();
        EnsureCurrentUserIncluded(staff);
        var invites = (await _staff.ListInvitesAsync(ct)).Where(i => i.IsPending).ToList();
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var stats = new StaffStatsDTO
        {
            TotalUsers = staff.Count,
            AddedThisMonth = staff.Count(s => s.CreatedAt != default && s.CreatedAt >= monthStart),
            ActiveCount = staff.Count(s => s.Status == StaffStatuses.Active),
            SuspendedCount = staff.Count(s => s.Status == StaffStatuses.Suspended),
            DeactivatedCount = staff.Count(s => s.Status == StaffStatuses.Deactivated),
            PendingInvites = invites.Count,
            ExpiringSoon = invites.Count(i => i.ExpiresAt <= DateTime.UtcNow.AddHours(48)),
            WaitingOver24h = invites.Count(i => i.CreatedAt <= DateTime.UtcNow.AddHours(-24)),
            AdminCount = CountRole(staff, StaffRoles.Admin),
            ManagerCount = CountRole(staff, StaffRoles.Manager),
            EngineerCount = CountRole(staff, StaffRoles.Engineer),
            SupportCount = CountRole(staff, StaffRoles.Support)
        };
        return Ok(Response<StaffStatsDTO>.SuccessResponse(stats, "Staff stats loaded"));
    }

    [HttpGet("invites")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<List<StaffInviteDTO>>>> ListInvites(CancellationToken ct)
    {
        await CloseOwnPendingInviteAsync(ct);
        var list = await _staff.ListInvitesAsync(ct);
        return Ok(Response<List<StaffInviteDTO>>.SuccessResponse(
            list.Select(ToInviteDto).ToList(),
            "Invites loaded"));
    }

    [HttpGet("export")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var list = (await _staff.ListAsync(ct)).Select(ToDto).ToList();
        EnsureCurrentUserIncluded(list);
        var sb = new StringBuilder();
        sb.AppendLine("Name,Email,Roles,Products,Status,Last Active");
        foreach (var s in list)
        {
            sb.AppendLine(string.Join(',',
                Csv(s.DisplayName ?? s.Email),
                Csv(s.Email),
                Csv(string.Join("; ", s.Roles)),
                Csv(string.Join("; ", s.ProductSlugs)),
                Csv(s.Status),
                Csv(s.LastActiveAt?.ToString("u") ?? "")));
        }

        var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd");
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"kobneti_users_{stamp}.csv");
    }

    [HttpGet("audit")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<List<AuditEventDTO>>>> AccessAudit(
        [FromQuery] int take = 200)
    {
        var tenant = _tenant.Current?.TenantId ?? "ops";
        var cap = Math.Clamp(take, 1, 500);
        var current = await _audit.SearchAsync(tenant, null, null, null, cap);
        var ops = current;
        if (!string.Equals(tenant, "ops", StringComparison.OrdinalIgnoreCase))
            ops = await _audit.SearchAsync("ops", null, null, null, cap);

        var merged = (current.Data ?? [])
            .Concat(ops.Data ?? [])
            .Where(IsAccessEvent)
            .GroupBy(e => e.Id)
            .Select(g => g.First())
            .OrderByDescending(e => e.CreatedAt)
            .Take(cap)
            .ToList();

        return Ok(Response<List<AuditEventDTO>>.SuccessResponse(merged, "Access audit loaded"));
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
                ct,
                dto.Roles,
                AdminRoleClaims.GetEmail(User) ?? AdminRoleClaims.GetDisplayName(User));

            if (dto.TeamId is { } teamId && teamId != Guid.Empty)
                await _teams.AddMemberAsync(teamId, created.Id, "member", ct);

            var (emailed, emailError) = await _inviteEmail.SendInviteAsync(
                created.Email,
                created.DisplayName,
                created.Role,
                dto.InviteRedirectUrl,
                ct);

            await _audit.WriteAsync(
                _tenant.Current?.TenantId ?? "ops",
                AuditActions.StaffInvite, "staff", created.Id.ToString(),
                AdminRoleClaims.GetUserId(User), AdminRoleClaims.GetDisplayName(User),
                null, StaffSnapshot(created, extra: new Dictionary<string, object?> { ["emailed"] = emailed, ["action"] = "invite" }));

            var payload = ToDto(created);
            payload.EmailSent = emailed;
            var message = emailed
                ? $"Invitation email sent to {created.Email}"
                : $"Staff was saved, but the email was not sent: {emailError}";
            return Ok(Response<StaffMemberDTO>.SuccessResponse(payload, message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(Response<StaffMemberDTO>.Fail(ex.Message));
        }
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<StaffMemberDTO>>> Deactivate(Guid id, CancellationToken ct) =>
        await SetStatusInternal(id, StaffStatuses.Deactivated, ct);

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<StaffMemberDTO>>> Activate(Guid id, CancellationToken ct) =>
        await SetStatusInternal(id, StaffStatuses.Active, ct);

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<StaffMemberDTO>>> SetStatus(
        Guid id,
        [FromBody] UpdateStaffStatusDTO dto,
        CancellationToken ct)
    {
        try
        {
            return await SetStatusInternal(id, StaffStatuses.Normalize(dto.Status), ct);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(Response<StaffMemberDTO>.Fail(ex.Message));
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<StaffMemberDTO>>> Update(
        Guid id,
        [FromBody] UpdateStaffProfileDTO dto,
        CancellationToken ct)
    {
        try
        {
            var previous = await FindStaffAsync(id, ct);
            var updated = await _staff.UpdateProfileAsync(id, dto.DisplayName, dto.Roles, dto.ProductSlugs, dto.Status, ct);
            if (updated is null)
                return NotFound(Response<StaffMemberDTO>.Fail("Staff not found"));
            await _audit.WriteAsync(_tenant.Current?.TenantId ?? "ops", AuditActions.StaffStatus, "staff", id.ToString(),
                AdminRoleClaims.GetUserId(User), AdminRoleClaims.GetDisplayName(User),
                previous is null ? null : StaffSnapshot(previous),
                StaffSnapshot(updated));
            return Ok(Response<StaffMemberDTO>.SuccessResponse(ToDto(updated), "Staff updated"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(Response<StaffMemberDTO>.Fail(ex.Message));
        }
    }

    [HttpPut("{id:guid}/products")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<StaffMemberDTO>>> SetProducts(
        Guid id,
        [FromBody] UpdateStaffProductsDTO dto,
        CancellationToken ct)
    {
        var previous = await FindStaffAsync(id, ct);
        var updated = await _staff.SetProductAccessAsync(id, dto.ProductSlugs ?? [], ct);
        if (updated is null)
            return NotFound(Response<StaffMemberDTO>.Fail("Staff not found"));
        await _audit.WriteAsync(_tenant.Current?.TenantId ?? "ops", AuditActions.StaffProducts, "staff", id.ToString(),
            AdminRoleClaims.GetUserId(User), AdminRoleClaims.GetDisplayName(User),
            previous is null ? null : StaffSnapshot(previous),
            StaffSnapshot(updated));
        return Ok(Response<StaffMemberDTO>.SuccessResponse(ToDto(updated), "Product access updated"));
    }

    [HttpPost("invites/{id:guid}/cancel")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<bool>>> CancelInvite(Guid id, CancellationToken ct)
    {
        var ok = await _staff.CancelInviteAsync(id, ct);
        if (!ok)
            return NotFound(Response<bool>.Fail("Invite not found"));
        await _audit.WriteAsync(_tenant.Current?.TenantId ?? "ops", AuditActions.StaffInvite, "staff-invite", id.ToString(),
            AdminRoleClaims.GetUserId(User), AdminRoleClaims.GetDisplayName(User),
            null, new { action = "revoke", inviteId = id });
        return Ok(Response<bool>.SuccessResponse(true, "Invite cancelled"));
    }

    [HttpPost("invites/{id:guid}/resend")]
    [Authorize(Policy = AdminAuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<Response<StaffInviteDTO>>> ResendInvite(
        Guid id,
        [FromBody] ResendStaffInviteDTO? dto,
        CancellationToken ct)
    {
        var invite = await _staff.ResendInviteAsync(id, ct);
        if (invite is null)
            return NotFound(Response<StaffInviteDTO>.Fail("Invite not found or no longer pending"));

        var (emailed, emailError) = await _inviteEmail.SendInviteAsync(
            invite.Email,
            invite.DisplayName,
            invite.Role,
            dto?.InviteRedirectUrl,
            ct);

        await _audit.WriteAsync(_tenant.Current?.TenantId ?? "ops", AuditActions.StaffInvite, "staff-invite", id.ToString(),
            AdminRoleClaims.GetUserId(User), AdminRoleClaims.GetDisplayName(User),
            null, new { action = "resend", email = invite.Email, displayName = invite.DisplayName, role = invite.Role, emailed });

        var payload = ToInviteDto(invite);
        payload.EmailSent = emailed;
        var message = emailed
            ? $"Invitation resent to {invite.Email}"
            : $"Invite is still open, but the email was not sent: {emailError}";
        return Ok(Response<StaffInviteDTO>.SuccessResponse(payload, message));
    }

    private async Task CloseOwnPendingInviteAsync(CancellationToken ct)
    {
        var email = AdminRoleClaims.GetEmail(User);
        if (string.IsNullOrWhiteSpace(email))
            return;
        try
        {
            await _staff.TouchLastActiveAsync(email, AdminRoleClaims.GetUserId(User), ct);
        }
        catch
        {
            // never block invite list on accept backfill
        }
    }

    private async Task<ActionResult<Response<StaffMemberDTO>>> SetStatusInternal(Guid id, string status, CancellationToken ct)
    {
        var previous = await FindStaffAsync(id, ct);
        var updated = await _staff.SetStatusAsync(id, status, ct);
        if (updated is null)
            return NotFound(Response<StaffMemberDTO>.Fail("Staff not found"));
        await _audit.WriteAsync(_tenant.Current?.TenantId ?? "ops", AuditActions.StaffStatus, "staff", id.ToString(),
            AdminRoleClaims.GetUserId(User), AdminRoleClaims.GetDisplayName(User),
            previous is null ? null : StaffSnapshot(previous),
            StaffSnapshot(updated, extra: new Dictionary<string, object?> { ["action"] = "status" }));
        return Ok(Response<StaffMemberDTO>.SuccessResponse(ToDto(updated), $"Staff {status}"));
    }

    private void EnsureCurrentUserIncluded(List<StaffMemberDTO> list)
    {
        var email = AdminRoleClaims.GetEmail(User);
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

        var role = User.FindFirstValue(AdminRoleClaims.RoleClaimType) ?? StaffRoles.Support;
        list.Add(new StaffMemberDTO
        {
            Id = AdminRoleClaims.GetUserId(User) ?? Guid.NewGuid(),
            Email = email,
            DisplayName = displayName,
            Role = role,
            Roles = [role],
            Status = StaffStatuses.Active,
            Active = true,
            ProductSlugs = AdminRoleClaims.GetProductSlugs(User)
                .Where(p => !string.Equals(p, AdminRoleClaims.AllProducts, StringComparison.Ordinal))
                .ToList(),
            CreatedAt = default,
            LastActiveAt = DateTime.UtcNow
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

    private static int CountRole(IReadOnlyList<StaffMemberDTO> staff, string role) =>
        staff.Count(s => s.Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase))
                         || string.Equals(s.Role, role, StringComparison.OrdinalIgnoreCase));

    private static StaffMemberDTO ToDto(StaffAccessRecord s)
    {
        var roles = s.Roles.Count > 0 ? s.Roles.ToList() : [s.Role];
        var status = StaffStatuses.Normalize(s.Status, s.Active);
        return new StaffMemberDTO
        {
            Id = s.Id,
            UserId = s.UserId,
            Email = s.Email,
            DisplayName = s.DisplayName,
            Role = StaffRoles.Primary(roles),
            Roles = roles,
            Status = status,
            Active = StaffStatuses.ToActiveFlag(status),
            ProductSlugs = s.ProductSlugs.ToList(),
            CreatedAt = s.CreatedAt,
            LastActiveAt = s.LastActiveAt
        };
    }

    private static StaffInviteDTO ToInviteDto(StaffInviteRecord i)
    {
        var now = DateTime.UtcNow;
        var pending = i.IsPending;
        string status;
        if (i.AcceptedAt is not null) status = "accepted";
        else if (i.CancelledAt is not null) status = "revoked";
        else if (i.ExpiresAt <= now) status = "expired";
        else if (i.ExpiresAt <= now.AddHours(48)) status = "expiring_soon";
        else status = "pending";

        return new StaffInviteDTO
        {
            Id = i.Id,
            StaffId = i.StaffId,
            Email = i.Email,
            DisplayName = i.DisplayName,
            Role = i.Role,
            Roles = i.Roles.ToList(),
            ProductSlugs = i.ProductSlugs.ToList(),
            InvitedByEmail = i.InvitedByEmail,
            CreatedAt = i.CreatedAt,
            ExpiresAt = i.ExpiresAt,
            AcceptedAt = i.AcceptedAt,
            CancelledAt = i.CancelledAt,
            Status = status,
            ExpiringSoon = pending && i.ExpiresAt <= now.AddHours(48),
            WaitingOver24h = pending && i.CreatedAt <= now.AddHours(-24)
        };
    }

    private static bool IsAccessEvent(AuditEventDTO e) =>
        e.Action.StartsWith("staff.", StringComparison.OrdinalIgnoreCase)
        || e.Action.StartsWith("auth.", StringComparison.OrdinalIgnoreCase)
        || e.EntityType is "staff" or "staff-invite" or "auth";

    private async Task<StaffAccessRecord?> FindStaffAsync(Guid id, CancellationToken ct) =>
        (await _staff.ListAsync(ct)).FirstOrDefault(s => s.Id == id);

    private static Dictionary<string, object?> StaffSnapshot(
        StaffAccessRecord s,
        Dictionary<string, object?>? extra = null)
    {
        var map = new Dictionary<string, object?>
        {
            ["email"] = s.Email,
            ["displayName"] = s.DisplayName,
            ["role"] = s.Role,
            ["roles"] = s.Roles,
            ["status"] = s.Status,
            ["productSlugs"] = s.ProductSlugs
        };
        if (extra is not null)
        {
            foreach (var kv in extra)
                map[kv.Key] = kv.Value;
        }
        return map;
    }

    private static string Csv(string value)
    {
        var v = value.Replace("\"", "\"\"");
        return $"\"{v}\"";
    }
}
