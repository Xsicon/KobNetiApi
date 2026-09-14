using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using KobNeti.Api.Auth;

namespace KobNeti.Api.Email;

public interface IStaffInviteEmailService
{
    Task<(bool Sent, string? Error)> SendInviteAsync(
        string email,
        string? displayName,
        string role,
        string? redirectTo,
        CancellationToken ct = default);
}

public sealed class StaffInviteEmailService : IStaffInviteEmailService
{
    private readonly IConfiguration _config;
    private readonly IEmailSender _email;
    private readonly PostmarkOptions _postmark;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IStaffAuthSync _authSync;
    private readonly ILogger<StaffInviteEmailService> _logger;

    public StaffInviteEmailService(
        IConfiguration config,
        IEmailSender email,
        IOptions<PostmarkOptions> postmark,
        IHttpClientFactory httpFactory,
        IStaffAuthSync authSync,
        ILogger<StaffInviteEmailService> logger)
    {
        _config = config;
        _email = email;
        _postmark = postmark.Value;
        _httpFactory = httpFactory;
        _authSync = authSync;
        _logger = logger;
    }

    public async Task<(bool Sent, string? Error)> SendInviteAsync(
        string email,
        string? displayName,
        string role,
        string? redirectTo,
        CancellationToken ct = default)
    {
        var to = email.Trim();
        if (string.IsNullOrWhiteSpace(to) || !to.Contains('@'))
            return (false, "Enter a valid work email address.");

        var redirect = FirstNonEmpty(
            redirectTo,
            _config["Auth:InviteRedirectUrl"],
            _config["Auth:PasswordResetRedirectUrl"],
            "https://localhost:5001/admin/reset-password");

        var grantRole = string.IsNullOrWhiteSpace(role) ? "support" : role.Trim();
        var name = string.IsNullOrWhiteSpace(displayName) ? to.Split('@')[0] : displayName.Trim();
        var roleLabel = grantRole;

        var supabaseUrl = (_config["Supabase:Url"] ?? "").TrimEnd('/');
        var serviceKey = _config["Supabase:ServiceRoleKey"]?.Trim() ?? "";

        if (_postmark.IsConfigured)
        {
            string? actionLink = null;
            if (!string.IsNullOrWhiteSpace(supabaseUrl) && !string.IsNullOrWhiteSpace(serviceKey))
            {
                // Invite for new Auth users; recovery if the email already exists.
                // Never use magiclink — that follows Supabase Site URL (Salguri).
                actionLink = await GenerateAppLinkAsync(supabaseUrl, serviceKey, to, "invite", redirect, name, grantRole, ct)
                             ?? await GenerateAppLinkAsync(supabaseUrl, serviceKey, to, "recovery", redirect, name, grantRole, ct);
            }

            var subject = "You're invited to KobNeti Operations";
            var (html, text) = BuildBodies(name, roleLabel, to, actionLink ?? redirect);
            var (ok, error) = await _email.SendAsync(to, subject, html, text, ct, "staff-invite");
            if (!ok)
            {
                _logger.LogWarning("Staff invite email not sent to {Email}: {Error}", to, error);
                return (false, error ?? "Failed to send invitation email.");
            }

            return (true, null);
        }

        if (!string.IsNullOrWhiteSpace(supabaseUrl) && !string.IsNullOrWhiteSpace(serviceKey))
        {
            var native = await InviteViaSupabaseAsync(supabaseUrl, serviceKey, to, name, redirect, ct);
            if (native.Ok)
                return (true, null);

            _logger.LogWarning("Supabase invite email failed for {Email}: {Error}", to, native.Error);
            return (false, native.Error ?? "Could not send the invitation email.");
        }

        return (false, "Email is not configured. Set Postmark:ServerToken (or Supabase:ServiceRoleKey with Auth email enabled) so invites can reach Gmail.");
    }

    private async Task<(bool Ok, string? Error)> InviteViaSupabaseAsync(
        string supabaseUrl,
        string serviceKey,
        string email,
        string displayName,
        string redirectTo,
        CancellationToken ct)
    {
        var http = _httpFactory.CreateClient("supabase-admin");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{supabaseUrl}/auth/v1/invite");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceKey);
        req.Headers.TryAddWithoutValidation("apikey", serviceKey);
        req.Content = JsonContent.Create(new
        {
            email,
            data = new { display_name = displayName },
            redirectTo
        });

        HttpResponseMessage res;
        try
        {
            res = await http.SendAsync(req, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Supabase invite HTTP failed");
            return (false, "Failed to reach Supabase Auth.");
        }

        if (res.IsSuccessStatusCode)
            return (true, null);

        var body = await res.Content.ReadAsStringAsync(ct);
        _logger.LogDebug("Supabase invite status {Status}: {Body}", (int)res.StatusCode, body);
        if ((int)res.StatusCode is 422 or 400)
        {
            var link = await GenerateAppLinkAsync(supabaseUrl, serviceKey, email, "recovery", redirectTo, displayName, "support", ct);
            if (!string.IsNullOrWhiteSpace(link))
                return (false, "This email already has an account. Ask them to sign in at the login page — Postmark is not configured to send a new link.");
        }

        return (false, "Supabase could not send the invite email. Configure Postmark:ServerToken to send from KobNeti.");
    }

    private async Task<string?> GenerateAppLinkAsync(
        string supabaseUrl,
        string serviceKey,
        string email,
        string type,
        string redirectTo,
        string displayName,
        string role,
        CancellationToken ct)
    {
        var http = _httpFactory.CreateClient("supabase-admin");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{supabaseUrl}/auth/v1/admin/generate_link");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceKey);
        req.Headers.TryAddWithoutValidation("apikey", serviceKey);
        req.Content = JsonContent.Create(new
        {
            type,
            email,
            data = new { display_name = displayName },
            options = new { redirect_to = redirectTo, data = new { display_name = displayName } }
        });

        try
        {
            var res = await http.SendAsync(req, ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogDebug("generate_link ({Type}) status {Status}: {Body}", type, (int)res.StatusCode, json);
                return null;
            }

            var appLink = SupabaseAuthLink.FromGenerateLinkJson(json, redirectTo);
            if (string.IsNullOrWhiteSpace(appLink))
            {
                _logger.LogWarning("generate_link ({Type}) returned no usable token for {Email}", type, email);
                return null;
            }

            await _authSync.TryGrantOpsRoleAsync(SupabaseAuthLink.TryReadUserId(json), email, role, ct);

            if (appLink.Contains("salguri", StringComparison.OrdinalIgnoreCase)
                || appLink.Contains("/auth/v1/verify", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Invite link for {Email} still points at an external Auth redirect ({Host}). Add KobNeti /admin/reset-password to Supabase Redirect URLs.",
                    email, new Uri(appLink).Host);
            }

            return appLink;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "generate_link ({Type}) failed", type);
        }

        return null;
    }

    private static (string Html, string Text) BuildBodies(string name, string role, string email, string actionUrl)
    {
        var safeName = WebUtility.HtmlEncode(name);
        var safeRole = WebUtility.HtmlEncode(role);
        var safeEmail = WebUtility.HtmlEncode(email);
        var safeUrl = WebUtility.HtmlEncode(actionUrl);
        var html = $"""
            <div style="font-family:Inter,Segoe UI,sans-serif;max-width:520px;margin:0 auto;color:#0F172A;">
              <h1 style="font-size:20px;margin:0 0 12px;">You're invited to KobNeti</h1>
              <p style="font-size:14px;line-height:1.5;color:#475569;">
                Hi {safeName}, you’ve been added as <strong>{safeRole}</strong> on the KobNeti Operations Platform
                ({safeEmail}).
              </p>
              <p style="margin:24px 0;">
                <a href="{safeUrl}"
                   style="display:inline-block;background:#6366F1;color:#fff;text-decoration:none;padding:12px 18px;border-radius:8px;font-weight:600;font-size:14px;">
                  Accept invitation
                </a>
              </p>
              <p style="font-size:12px;color:#64748B;line-height:1.5;">
                This link expires soon. If you were not expecting this email, you can ignore it.
              </p>
              <p style="font-size:12px;color:#94A3B8;">— KobNeti Operations</p>
            </div>
            """;
        var text =
            $"You're invited to KobNeti Operations\n\nYou've been added as {role} ({email}).\nOpen this link to accept:\n{actionUrl}\n";
        return (html, text);
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))!.Trim();
}
