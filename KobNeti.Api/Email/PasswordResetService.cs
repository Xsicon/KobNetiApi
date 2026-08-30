using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace KobNeti.Api.Email;

public interface IPasswordResetService
{
    /// <summary>
    /// Always safe to call. Does not reveal whether the email exists.
    /// </summary>
    Task<PasswordResetResult> RequestResetAsync(string email, string redirectTo, CancellationToken ct = default);
}

public sealed record PasswordResetResult(bool Accepted, bool EmailQueued, string Message);

public sealed class PasswordResetService : IPasswordResetService
{
    private readonly IConfiguration _config;
    private readonly IEmailSender _email;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        IConfiguration config,
        IEmailSender email,
        IHttpClientFactory httpFactory,
        ILogger<PasswordResetService> logger)
    {
        _config = config;
        _email = email;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<PasswordResetResult> RequestResetAsync(
        string email,
        string redirectTo,
        CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var publicMessage = "If an account exists for that email, a reset link has been sent.";

        if (string.IsNullOrWhiteSpace(normalized) || !normalized.Contains('@'))
            return new PasswordResetResult(false, false, "Enter a valid work email address.");

        var supabaseUrl = (_config["Supabase:Url"] ?? "").TrimEnd('/');
        var serviceKey = _config["Supabase:ServiceRoleKey"]?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceKey))
        {
            _logger.LogWarning(
                "Password reset skipped: Supabase ServiceRoleKey (or Url) is not configured.");
            return new PasswordResetResult(true, false, publicMessage);
        }

        string? actionLink;
        try
        {
            actionLink = await GenerateRecoveryLinkAsync(supabaseUrl, serviceKey, normalized, redirectTo, ct);
        }
        catch (Exception ex)
        {
            // Do not reveal account existence; log for ops.
            _logger.LogWarning(ex, "Supabase generate_link failed for password reset");
            return new PasswordResetResult(true, false, publicMessage);
        }

        if (string.IsNullOrWhiteSpace(actionLink))
        {
            // Unknown user or generate_link returned empty — still generic success.
            return new PasswordResetResult(true, false, publicMessage);
        }

        var subject = "Reset your KobNeti password";
        var html = $"""
            <div style="font-family:Inter,Segoe UI,sans-serif;max-width:520px;margin:0 auto;color:#0F172A;">
              <h1 style="font-size:20px;margin:0 0 12px;">Reset your password</h1>
              <p style="font-size:14px;line-height:1.5;color:#475569;">
                We received a request to reset the password for <strong>{System.Net.WebUtility.HtmlEncode(normalized)}</strong>.
              </p>
              <p style="margin:24px 0;">
                <a href="{System.Net.WebUtility.HtmlEncode(actionLink)}"
                   style="display:inline-block;background:#6366F1;color:#fff;text-decoration:none;padding:12px 18px;border-radius:8px;font-weight:600;font-size:14px;">
                  Reset password
                </a>
              </p>
              <p style="font-size:12px;color:#64748B;line-height:1.5;">
                This link expires soon. If you did not request a reset, you can ignore this email.
              </p>
              <p style="font-size:12px;color:#94A3B8;">— KobNeti Operations</p>
            </div>
            """;
        var text =
            $"Reset your KobNeti password\n\nOpen this link to choose a new password:\n{actionLink}\n\nIf you did not request this, ignore this email.";

        var (ok, error) = await _email.SendAsync(normalized, subject, html, text, ct);
        if (!ok)
        {
            _logger.LogWarning("Password reset email not sent: {Error}", error);
            return new PasswordResetResult(true, false, publicMessage);
        }

        return new PasswordResetResult(true, true, publicMessage);
    }

    private async Task<string?> GenerateRecoveryLinkAsync(
        string supabaseUrl,
        string serviceKey,
        string email,
        string redirectTo,
        CancellationToken ct)
    {
        var http = _httpFactory.CreateClient("supabase-admin");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{supabaseUrl}/auth/v1/admin/generate_link");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceKey);
        req.Headers.TryAddWithoutValidation("apikey", serviceKey);
        req.Content = JsonContent.Create(new
        {
            type = "recovery",
            email,
            options = new { redirect_to = redirectTo }
        });

        var res = await http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            _logger.LogDebug("generate_link status {Status}: {Body}", (int)res.StatusCode, json);
            // 404 / user not found → treat as no link
            return null;
        }

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("action_link", out var linkEl))
            return linkEl.GetString();

        // Some responses nest under "properties"
        if (doc.RootElement.TryGetProperty("properties", out var props)
            && props.TryGetProperty("action_link", out var nested))
            return nested.GetString();

        return null;
    }
}

public sealed class ForgotPasswordRequest
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    [JsonPropertyName("redirectTo")]
    public string? RedirectTo { get; set; }
}
