using System.Text.Json;

namespace KobNeti.Api.Email;

/// <summary>
/// Builds a first-party KobNeti URL from Supabase generate_link JSON so the
/// user never lands on this project's Site URL (currently Salguri).
/// </summary>
public static class SupabaseAuthLink
{
    public static string? FromGenerateLinkJson(string json, string appRedirectUrl)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(appRedirectUrl))
            return null;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var props = root.TryGetProperty("properties", out var nested) ? nested : root;

        var hashed = Read(props, "hashed_token") ?? Read(root, "hashed_token");
        var type = Read(props, "verification_type")
                   ?? Read(root, "verification_type")
                   ?? TypeFromActionLink(Read(props, "action_link") ?? Read(root, "action_link"));

        if (!string.IsNullOrWhiteSpace(hashed))
            return WithTokenHash(appRedirectUrl, hashed, NormalizeType(type));

        var action = Read(props, "action_link") ?? Read(root, "action_link");
        if (string.IsNullOrWhiteSpace(action))
            return null;

        return RewriteRedirectTo(action, appRedirectUrl);
    }

    public static string WithTokenHash(string appRedirectUrl, string tokenHash, string type)
    {
        var baseUrl = appRedirectUrl.Trim();
        var hashAt = baseUrl.IndexOf('#');
        if (hashAt >= 0)
            baseUrl = baseUrl[..hashAt];

        var sep = baseUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{baseUrl}{sep}token_hash={Uri.EscapeDataString(tokenHash)}&type={Uri.EscapeDataString(NormalizeType(type))}";
    }

    public static string RewriteRedirectTo(string actionLink, string appRedirectUrl)
    {
        var uri = new Uri(actionLink);
        var builder = new UriBuilder(uri) { Port = uri.IsDefaultPort ? -1 : uri.Port };
        var parts = (builder.Query ?? "").TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.StartsWith("redirect_to=", StringComparison.OrdinalIgnoreCase))
            .ToList();
        parts.Add("redirect_to=" + Uri.EscapeDataString(appRedirectUrl.Trim()));
        builder.Query = string.Join('&', parts);
        return builder.Uri.ToString();
    }

    public static string NormalizeType(string? type)
    {
        var t = (type ?? "recovery").Trim().ToLowerInvariant();
        return t switch
        {
            "invite" or "signup" or "recovery" or "magiclink" or "email" or "email_change" => t,
            _ => "recovery"
        };
    }

    public static Guid? TryReadUserId(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("id", out var idEl)
                && Guid.TryParse(idEl.GetString(), out var id))
                return id;
        }
        catch
        {
            // ignore
        }
        return null;
    }

    private static string? TypeFromActionLink(string? actionLink)
    {
        if (string.IsNullOrWhiteSpace(actionLink) || !Uri.TryCreate(actionLink, UriKind.Absolute, out var uri))
            return null;
        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals("type", StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(kv[1]);
        }
        return null;
    }

    private static string? Read(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object
        && el.TryGetProperty(name, out var v)
        && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
