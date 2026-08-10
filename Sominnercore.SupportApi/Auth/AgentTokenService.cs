using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sominnercore.SupportApi.Tenancy;

namespace Sominnercore.SupportApi.Auth;

public interface IAgentTokenService
{
    Task<(bool Ok, string? Token, string? Error)> ExchangeSupabaseTokenAsync(string supabaseAccessToken);
    string CreateAgentToken(Guid userId, string email, string role, string? userName);
}

public class AgentTokenService : IAgentTokenService
{
    private readonly SupportOptions _options;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public AgentTokenService(
        IOptions<SupportOptions> options,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<(bool Ok, string? Token, string? Error)> ExchangeSupabaseTokenAsync(string supabaseAccessToken)
    {
        if (string.IsNullOrWhiteSpace(supabaseAccessToken))
            return (false, null, "Access token is required.");

        var supabaseUrl = _configuration["Supabase:Url"]?.TrimEnd('/');
        var anonKey = _configuration["Supabase:AnonKey"];
        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(anonKey))
            return (false, null, "Supabase is not configured on Support API.");

        // Dev/test bypass: accept "dev:<email>" when UseInMemoryStore
        if (_options.UseInMemoryStore && supabaseAccessToken.StartsWith("dev:", StringComparison.OrdinalIgnoreCase))
        {
            var email = supabaseAccessToken["dev:".Length..].Trim();
            if (!IsCoreAdminEmail(email) && _options.CoreAdminEmails.Length > 0)
                return (false, null, "Not an admin.");
            var token = CreateAgentToken(Guid.NewGuid(), email, AdminRoles.Admin, email);
            return (true, token, null);
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

            var isAdmin = IsAdminFromUserJson(root) || IsCoreAdminEmail(email);
            if (!isAdmin)
                return (false, null,
                    $"User is not a Core admin ({email}). Set Support:CoreAdminEmails on the API or app_metadata.role=admin.");

            var token = CreateAgentToken(userId, email, AdminRoles.Admin, email);
            return (true, token, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Exchange failed: {ex.Message}");
        }
    }

    public string CreateAgentToken(Guid userId, string email, string role, string? userName)
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
