using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KobNeti.Api.Staff;

namespace KobNeti.Api.Auth;

public interface IStaffAuthSync
{
    /// <summary>
    /// Writes staff role into Auth app_metadata so KobNeti login (JWT check) accepts invited users.
    /// </summary>
    Task TryGrantOpsRoleAsync(Guid? userId, string email, string role, CancellationToken ct = default);
}

public sealed class StaffAuthSync : IStaffAuthSync
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<StaffAuthSync> _logger;

    public StaffAuthSync(
        IConfiguration config,
        IHttpClientFactory httpFactory,
        ILogger<StaffAuthSync> logger)
    {
        _config = config;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task TryGrantOpsRoleAsync(Guid? userId, string email, string role, CancellationToken ct = default)
    {
        var supabaseUrl = (_config["Supabase:Url"] ?? "").TrimEnd('/');
        var serviceKey = _config["Supabase:ServiceRoleKey"]?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceKey))
            return;

            var opsRole = StaffRoles.All.Contains(role)
                ? StaffRoles.All.First(x => string.Equals(x, role.Trim(), StringComparison.OrdinalIgnoreCase))
                : StaffRoles.Support;
        var http = _httpFactory.CreateClient("supabase-admin");

        try
        {
            var id = userId is { } uid && uid != Guid.Empty
                ? uid
                : await FindUserIdByEmailAsync(http, supabaseUrl, serviceKey, email, ct);
            if (id is null)
            {
                _logger.LogWarning("Could not grant ops role; Auth user not found for {Email}", email);
                return;
            }

            using var get = AdminRequest(HttpMethod.Get, $"{supabaseUrl}/auth/v1/admin/users/{id}", serviceKey);
            using var getRes = await http.SendAsync(get, ct);
            if (!getRes.IsSuccessStatusCode)
            {
                _logger.LogWarning("Auth user GET failed for {UserId}: {Status}", id, (int)getRes.StatusCode);
                return;
            }

            using var doc = JsonDocument.Parse(await getRes.Content.ReadAsStringAsync(ct));
            var meta = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (doc.RootElement.TryGetProperty("app_metadata", out var existing)
                && existing.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in existing.EnumerateObject())
                    meta[p.Name] = JsonSerializer.Deserialize<object>(p.Value.GetRawText());
            }

            meta["role"] = opsRole;
            meta["kobneti_staff"] = true;

            using var put = AdminRequest(HttpMethod.Put, $"{supabaseUrl}/auth/v1/admin/users/{id}", serviceKey);
            put.Content = JsonContent.Create(new { app_metadata = meta });
            using var putRes = await http.SendAsync(put, ct);
            if (!putRes.IsSuccessStatusCode)
            {
                var body = await putRes.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Auth app_metadata update failed for {Email}: {Status} {Body}",
                    email, (int)putRes.StatusCode, body.Length > 180 ? body[..180] : body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to grant ops Auth role for {Email}", email);
        }
    }

    private static async Task<Guid?> FindUserIdByEmailAsync(
        HttpClient http,
        string supabaseUrl,
        string serviceKey,
        string email,
        CancellationToken ct)
    {
        var url = $"{supabaseUrl}/auth/v1/admin/users?page=1&per_page=200";
        using var req = AdminRequest(HttpMethod.Get, url, serviceKey);
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
            return null;

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("users", out var users)
            || users.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var user in users.EnumerateArray())
        {
            var userEmail = user.TryGetProperty("email", out var el) ? el.GetString() : null;
            if (!string.Equals(userEmail, email, StringComparison.OrdinalIgnoreCase))
                continue;
            if (user.TryGetProperty("id", out var idEl) && Guid.TryParse(idEl.GetString(), out var id))
                return id;
        }

        return null;
    }

    private static HttpRequestMessage AdminRequest(HttpMethod method, string url, string serviceKey)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceKey);
        req.Headers.TryAddWithoutValidation("apikey", serviceKey);
        return req;
    }
}
