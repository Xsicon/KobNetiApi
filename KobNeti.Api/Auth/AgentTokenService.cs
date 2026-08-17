using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using KobNeti.Api.Products;
using KobNeti.Api.Staff;
using KobNeti.Api.Tenancy;

namespace KobNeti.Api.Auth;

public interface IAgentTokenService
{
    Task<(bool Ok, string? Token, string? Error)> ExchangeSupabaseTokenAsync(string supabaseAccessToken);
    string CreateAgentToken(
        Guid userId,
        string email,
        string role,
        string? userName,
        IEnumerable<string>? productSlugs = null);
}

public class AgentTokenService : IAgentTokenService
{
    private readonly SupportOptions _options;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IStaffDirectory _staff;
    private readonly IProductRegistry _products;

    public AgentTokenService(
        IOptions<SupportOptions> options,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IStaffDirectory staff,
        IProductRegistry products)
    {
        _options = options.Value;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _staff = staff;
        _products = products;
    }

    public async Task<(bool Ok, string? Token, string? Error)> ExchangeSupabaseTokenAsync(string supabaseAccessToken)
    {
        if (string.IsNullOrWhiteSpace(supabaseAccessToken))
            return (false, null, "Access token is required.");

        var supabaseUrl = _configuration["Supabase:Url"]?.TrimEnd('/');
        var anonKey = _configuration["Supabase:AnonKey"];
        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(anonKey))
            return (false, null, "Supabase is not configured on Support API.");

        if (_options.UseInMemoryStore && supabaseAccessToken.StartsWith("dev:", StringComparison.OrdinalIgnoreCase))
        {
            var email = supabaseAccessToken["dev:".Length..].Trim();
            return await IssueForEmailAsync(Guid.NewGuid(), email, email);
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{supabaseUrl}/auth/v1/user");
            req.Headers.TryAddWithoutValidation("apikey", anonKey);
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {supabaseAccessToken}");

            using var res = await client.SendAsync(req);
            if (!res.IsSuccessStatusCode)
            {
                var detail = await res.Content.ReadAsStringAsync();
                if (detail.Length > 180)
                    detail = detail[..180];
                return (false, null,
                    string.IsNullOrWhiteSpace(detail)
                        ? $"Invalid Supabase session ({(int)res.StatusCode})."
                        : $"Invalid Supabase session ({(int)res.StatusCode}): {detail}");
            }

            await using var stream = await res.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;

            var email = root.TryGetProperty("email", out var emailEl) ? emailEl.GetString() ?? "" : "";
            var idRaw = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            var userId = Guid.TryParse(idRaw, out var parsed) ? parsed : Guid.NewGuid();

            return await IssueForEmailAsync(userId, email, email, root);
        }
        catch (Exception ex)
        {
            return (false, null, $"Exchange failed: {ex.Message}");
        }
    }

    private async Task<(bool Ok, string? Token, string? Error)> IssueForEmailAsync(
        Guid userId,
        string email,
        string? userName,
        JsonElement? supabaseUser = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (false, null, "Email is required.");

        var staff = await _staff.FindByEmailAsync(email);
        var isCoreAdmin = IsCoreAdminEmail(email)
                          || (supabaseUser is { } root && IsAdminFromUserJson(root));

        string role;
        List<string> products;

        if (isCoreAdmin || (staff is not null && string.Equals(staff.Role, StaffRoles.Admin, StringComparison.OrdinalIgnoreCase)))
        {
            role = StaffRoles.Admin;
            products = [AdminRoleClaims.AllProducts];
        }
        else if (staff is not null && staff.Active)
        {
            role = staff.Role;
            if (!StaffRoles.CanUseSupportApis(role) && !string.Equals(role, StaffRoles.Engineer, StringComparison.OrdinalIgnoreCase))
                return (false, null, $"Role '{role}' is not allowed.");

            if (!StaffRoles.CanUseSupportApis(role))
                return (false, null, $"Role '{role}' cannot access Support APIs.");

            products = staff.ProductSlugs.ToList();
            if (products.Count == 0)
            {
                // Manager/support with no assignments: no products (empty list). Prefer explicit grants.
                var all = await _products.ListEnabledAsync();
                if (string.Equals(role, StaffRoles.Manager, StringComparison.OrdinalIgnoreCase))
                    products = all.Select(p => p.Slug).ToList();
            }

            userName = string.IsNullOrWhiteSpace(staff.DisplayName) ? userName : staff.DisplayName;
        }
        else if (isCoreAdmin)
        {
            role = StaffRoles.Admin;
            products = [AdminRoleClaims.AllProducts];
        }
        else
        {
            return (false, null,
                $"User is not KobNeti staff ({email}). Add staff_profiles row or Support:CoreAdminEmails / Support:Staff.");
        }

        if (!StaffRoles.CanUseSupportApis(role))
            return (false, null, $"Role '{role}' cannot access Support APIs.");

        var token = CreateAgentToken(userId, email, role, userName, products);
        return (true, token, null);
    }

    public string CreateAgentToken(
        Guid userId,
        string email,
        string role,
        string? userName,
        IEnumerable<string>? productSlugs = null)
    {
        var secret = _options.CoreAgentJwtSecret
                     ?? throw new InvalidOperationException("Support:CoreAgentJwtSecret is not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(AdminRoleClaims.RoleClaimType, role)
        };

        if (!string.IsNullOrWhiteSpace(userName))
            claims.Add(new Claim(ClaimTypes.Name, userName));

        foreach (var slug in (productSlugs ?? []).Where(s => !string.IsNullOrWhiteSpace(s)))
            claims.Add(new Claim(AdminRoleClaims.ProductClaimType, slug.Trim()));

        var jwt = new JwtSecurityToken(
            claims: claims,
            notBefore: now,
            expires: now.AddHours(12),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private bool IsCoreAdminEmail(string email) =>
        _options.CoreAdminEmails.Any(e =>
            string.Equals(e, email, StringComparison.OrdinalIgnoreCase));

    private static bool IsAdminFromUserJson(JsonElement root)
    {
        if (!root.TryGetProperty("app_metadata", out var meta))
            return false;

        if (meta.TryGetProperty("role", out var role)
            && string.Equals(role.GetString(), "admin", StringComparison.OrdinalIgnoreCase))
            return true;

        if (meta.TryGetProperty("is_admin", out var isAdmin))
        {
            if (isAdmin.ValueKind == JsonValueKind.True) return true;
            if (isAdmin.ValueKind == JsonValueKind.String
                && (isAdmin.GetString() is "true" or "1"))
                return true;
        }

        return false;
    }
}
