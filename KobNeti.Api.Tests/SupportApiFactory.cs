using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using KobNeti.Api.Auth;

namespace KobNeti.Api.Tests;

public class SupportApiFactory : WebApplicationFactory<Program>
{
    public const string TenantAKey = "pk_tenant_a";
    public const string TenantBKey = "pk_tenant_b";
    public const string TenantASecret = "tenant-a-jwt-secret-at-least-32-chars!!";
    public const string TenantBSecret = "tenant-b-jwt-secret-at-least-32-chars!!";
    public const string CoreSecret = "core-agent-jwt-secret-at-least-32-chars!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Support:UseInMemoryStore"] = "true",
                ["Support:CoreAgentJwtSecret"] = CoreSecret,
                ["Support:CoreAdminEmails:0"] = "admin@test.local",
                ["Support:Tenants:muuqwear:DisplayName"] = "MuuqWear",
                ["Support:Tenants:muuqwear:PublicKey"] = TenantAKey,
                ["Support:Tenants:muuqwear:JwtSecret"] = TenantASecret,
                ["Support:Tenants:muuqwear:UpstreamApiBaseUrl"] = "",
                ["Support:Tenants:muuqwear:Enabled"] = "true",
                ["Support:Tenants:salguri:DisplayName"] = "Salguri",
                ["Support:Tenants:salguri:PublicKey"] = TenantBKey,
                ["Support:Tenants:salguri:JwtSecret"] = TenantBSecret,
                ["Support:Tenants:salguri:UpstreamApiBaseUrl"] = "",
                ["Support:Tenants:salguri:Enabled"] = "true",
                ["Support:Tenants:gaarx:Enabled"] = "false",
                ["Supabase:ServiceRoleKey"] = ""
            });
        });
    }

    public HttpClient CreateTenantClient(string tenantKey, string? bearer = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Remove("X-Tenant-Key");
        client.DefaultRequestHeaders.Add("X-Tenant-Key", tenantKey);
        if (!string.IsNullOrWhiteSpace(bearer))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return client;
    }

    public static string CreateAgentToken(string secret, string role = AdminRoles.Admin, params string[] productSlugs)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, "agent@test.local"),
            new(AdminRoleClaims.RoleClaimType, role),
            new(ClaimTypes.Name, "Test Agent")
        };
        var products = productSlugs.Length > 0 ? productSlugs : [AdminRoleClaims.AllProducts];
        foreach (var p in products)
            claims.Add(new Claim(AdminRoleClaims.ProductClaimType, p));
        var token = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddHours(1), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
