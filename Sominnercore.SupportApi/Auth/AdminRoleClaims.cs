using System.Security.Claims;

namespace Sominnercore.SupportApi.Auth;

public static class AdminRoles
{
    public const string Admin = "admin";
    public const string SupportTeam = "support_team";
}

public static class AdminAuthorizationPolicies
{
    public const string AdminSupport = "AdminSupport";
}

public static class AdminRoleClaims
{
    public const string RoleClaimType = "app_role";

    public static bool CanActAsChatAdmin(ClaimsPrincipal user)
    {
        if (user.IsInRole(AdminRoles.Admin) || user.IsInRole(AdminRoles.SupportTeam))
            return true;

        foreach (var claim in user.FindAll(RoleClaimType).Concat(user.FindAll(ClaimTypes.Role)))
        {
            if (string.Equals(claim.Value, AdminRoles.Admin, StringComparison.OrdinalIgnoreCase)
                || string.Equals(claim.Value, AdminRoles.SupportTeam, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static Guid? GetUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? user.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static string GetDisplayName(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Name)
        ?? user.FindFirstValue("email")
        ?? user.FindFirstValue(ClaimTypes.Email)
        ?? "Support Agent";
}
