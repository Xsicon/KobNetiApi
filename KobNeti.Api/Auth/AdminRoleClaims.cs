using System.Security.Claims;
using KobNeti.Api.Staff;

namespace KobNeti.Api.Auth;

public static class AdminRoles
{
    public const string Admin = StaffRoles.Admin;
    public const string SupportTeam = "support_team"; // legacy claim value
}

public static class AdminAuthorizationPolicies
{
    public const string AdminSupport = "AdminSupport";
}

public static class AdminRoleClaims
{
    public const string RoleClaimType = "app_role";
    public const string ProductClaimType = "product";
    public const string AllProducts = "*";

    public static bool CanActAsChatAdmin(ClaimsPrincipal user)
    {
        foreach (var claim in user.FindAll(RoleClaimType).Concat(user.FindAll(ClaimTypes.Role)))
        {
            if (StaffRoles.CanUseSupportApis(claim.Value))
                return true;
        }

        return user.IsInRole(AdminRoles.Admin)
               || user.IsInRole(AdminRoles.SupportTeam)
               || user.IsInRole(StaffRoles.Manager)
               || user.IsInRole(StaffRoles.Support);
    }

    public static bool IsPlatformAdmin(ClaimsPrincipal user) =>
        user.FindAll(RoleClaimType).Concat(user.FindAll(ClaimTypes.Role))
            .Any(c => string.Equals(c.Value, StaffRoles.Admin, StringComparison.OrdinalIgnoreCase))
        || user.IsInRole(StaffRoles.Admin);

    public static IReadOnlyList<string> GetProductSlugs(ClaimsPrincipal user)
    {
        return user.FindAll(ProductClaimType)
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool CanAccessProduct(ClaimsPrincipal user, string productSlug)
    {
        if (IsPlatformAdmin(user))
            return true;

        var products = GetProductSlugs(user);
        if (products.Any(p => p == AllProducts))
            return true;

        return products.Any(p => string.Equals(p, productSlug, StringComparison.OrdinalIgnoreCase));
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
