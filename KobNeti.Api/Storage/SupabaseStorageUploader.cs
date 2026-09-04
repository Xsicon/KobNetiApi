using System.Net.Http.Headers;

namespace KobNeti.Api.Storage;

public interface ISupabaseStorageUploader
{
    bool IsConfigured { get; }
    Task<(bool Ok, string? PublicUrl, string? Error)> UploadAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken ct = default);
}

public sealed class SupabaseStorageUploader : ISupabaseStorageUploader
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public SupabaseStorageUploader(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_config["Supabase:Url"])
        && !string.IsNullOrWhiteSpace(_config["Supabase:ServiceRoleKey"]);

    public async Task<(bool Ok, string? PublicUrl, string? Error)> UploadAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken ct = default)
    {
        var baseUrl = _config["Supabase:Url"]?.TrimEnd('/');
        var serviceKey = _config["Supabase:ServiceRoleKey"]?.Trim() ?? "";
        var bucket = _config["Supabase:OpsFilesBucket"]?.Trim() ?? "ops-files";

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(serviceKey))
            return (false, null, "Supabase storage is not configured (ServiceRoleKey required).");

        var key = objectKey.Trim().TrimStart('/');
        var client = _httpClientFactory.CreateClient("supabase-admin");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl}/storage/v1/object/{bucket}/{key}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceKey);
        request.Headers.TryAddWithoutValidation("x-upsert", "true");
        request.Content = new StreamContent(content);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (body.Contains("NoSuchBucket", StringComparison.OrdinalIgnoreCase)
                || body.Contains("Bucket not found", StringComparison.OrdinalIgnoreCase))
            {
                return (false, null,
                    "Supabase bucket 'ops-files' not found. Create it in Dashboard → Storage → New bucket (name: ops-files, public), or run supabase/storage_buckets.sql.");
            }

            return (false, null, $"Storage upload failed ({(int)response.StatusCode}): {body}");
        }

        var publicUrl = $"{baseUrl}/storage/v1/object/public/{bucket}/{key}";
        return (true, publicUrl, null);
    }
}
