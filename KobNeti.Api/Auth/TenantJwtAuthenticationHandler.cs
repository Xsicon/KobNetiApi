using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using KobNeti.Api.Tenancy;

namespace KobNeti.Api.Auth;

public class TenantJwtAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TenantJwt";

    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly SupportOptions _supportOptions;

    public TenantJwtAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ITenantContextAccessor tenantAccessor,
        IOptions<SupportOptions> supportOptions)
        : base(options, logger, encoder)
    {
        _tenantAccessor = tenantAccessor;
        _supportOptions = supportOptions.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header)
            || string.IsNullOrWhiteSpace(header))
            return Task.FromResult(AuthenticateResult.NoResult());

        var value = header.ToString();
        if (!value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var token = value["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
            return Task.FromResult(AuthenticateResult.NoResult());

        var secrets = new List<string>();
        if (_tenantAccessor.Current is { } tenant && !string.IsNullOrWhiteSpace(tenant.JwtSecret))
            secrets.Add(tenant.JwtSecret);
        if (!string.IsNullOrWhiteSpace(_supportOptions.CoreAgentJwtSecret))
            secrets.Add(_supportOptions.CoreAgentJwtSecret);

        if (secrets.Count == 0)
            return Task.FromResult(AuthenticateResult.Fail("No JWT secrets configured."));

        Exception? lastError = null;
        foreach (var secret in secrets.Distinct())
        {
            try
            {
                var principal = Validate(token, secret);
                var ticket = new AuthenticationTicket(principal, SchemeName);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        return Task.FromResult(AuthenticateResult.Fail(lastError?.Message ?? "Invalid token."));
    }

    private static ClaimsPrincipal Validate(string token, string secret)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            RoleClaimType = AdminRoleClaims.RoleClaimType,
            NameClaimType = ClaimTypes.Name
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.ValidateToken(token, parameters, out _);
    }
}
