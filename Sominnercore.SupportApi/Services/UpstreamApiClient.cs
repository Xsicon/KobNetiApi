using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Sominnercore.SupportApi.Auth;
using Sominnercore.SupportApi.Shared;
using Sominnercore.SupportApi.Tenancy;

namespace Sominnercore.SupportApi.Services;

public class UpstreamApiClient
{
    private readonly ITenantResolver _tenants;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UpstreamApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public UpstreamApiClient(
        ITenantResolver tenants,
        IHttpClientFactory httpClientFactory,
        ILogger<UpstreamApiClient> logger)
    {
        _tenants = tenants;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool TryGetUpstream(string tenantId, out string? baseUrl, out string? jwtSecret)
    {
        baseUrl = null;
        jwtSecret = null;
        if (!_tenants.TryGetById(tenantId, out var tenant) || tenant is null)
            return false;

        if (string.IsNullOrWhiteSpace(tenant.UpstreamApiBaseUrl))
            return false;

        baseUrl = tenant.UpstreamApiBaseUrl.TrimEnd('/') + "/";
        jwtSecret = tenant.JwtSecret;
        return true;
    }

    public async Task<Response<T>> ForwardAsync<T>(
        string tenantId,
        string relativePath,
        HttpMethod method,
        object? body = null,
        bool mintAdminToken = true)
    {
        if (!TryGetUpstream(tenantId, out var baseUrl, out var jwtSecret))
            return Response<T>.Fail($"No upstream configured for tenant '{tenantId}'.");

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var req = new HttpRequestMessage(method, baseUrl + relativePath);

            if (mintAdminToken)
            {
                if (string.IsNullOrWhiteSpace(jwtSecret))
                {
                    return Response<T>.Fail(
                        $"Tenant '{tenantId}' UpstreamApiBaseUrl is set but JwtSecret is missing.");
                }

                req.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer", MintUpstreamAdminToken(jwtSecret));
            }

            if (body is not null)
                req.Content = JsonContent.Create(body);

            using var res = await client.SendAsync(req);
            var payload = await res.Content.ReadFromJsonAsync<Response<T>>(JsonOptions);
            if (payload is not null)
                return payload;

            var raw = await res.Content.ReadAsStringAsync();
            _logger.LogWarning(
                "Upstream {Path} returned {Status}: {Body}",
                relativePath, (int)res.StatusCode, raw);
            return Response<T>.Fail(
                $"Upstream error ({(int)res.StatusCode}). Is MuuqWearApi running on {baseUrl}?");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upstream forward failed for {Tenant} {Path}", tenantId, relativePath);
            return Response<T>.Fail($"Unable to reach upstream: {ex.Message}");
        }
    }

    private static string MintUpstreamAdminToken(string jwtSecret, Guid? preferredUserId = null)
    {
        // MuuqWear ChatController treats Guid.Empty as "no user" and then requires GuestName
        // even for admins — never mint Empty.
        var userId = preferredUserId is { } id && id != Guid.Empty
            ? id
            : Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, "support-bridge@sominnercore.local"),
            new Claim(AdminRoleClaims.RoleClaimType, AdminRoles.Admin),
            new Claim(ClaimTypes.Name, "Support Bridge")
        };

        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: now,
            expires: now.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
