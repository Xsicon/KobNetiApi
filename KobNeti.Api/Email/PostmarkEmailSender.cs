namespace KobNeti.Api.Email;

public class PostmarkOptions
{
    public const string SectionName = "Postmark";

    /// <summary>Server API token. Use POSTMARK_API_TEST for dry-run validation.</summary>
    public string ServerToken { get; set; } = "";

    /// <summary>Verified sender, e.g. KobNeti Ops &lt;noreply@kobneti.com&gt;</summary>
    public string FromEmail { get; set; } = "KobNeti Ops <noreply@kobneti.com>";

    public string MessageStream { get; set; } = "outbound";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ServerToken);
}

public interface IEmailSender
{
    Task<(bool Ok, string? Error)> SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken ct = default);
}

public sealed class PostmarkEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly PostmarkOptions _options;
    private readonly ILogger<PostmarkEmailSender> _logger;

    public PostmarkEmailSender(
        HttpClient http,
        Microsoft.Extensions.Options.IOptions<PostmarkOptions> options,
        ILogger<PostmarkEmailSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        _http.BaseAddress ??= new Uri("https://api.postmarkapp.com/");
    }

    public async Task<(bool Ok, string? Error)> SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
            return (false, "Postmark is not configured (Postmark:ServerToken).");

        using var req = new HttpRequestMessage(HttpMethod.Post, "email");
        req.Headers.TryAddWithoutValidation("Accept", "application/json");
        req.Headers.TryAddWithoutValidation("X-Postmark-Server-Token", _options.ServerToken.Trim());
        req.Content = JsonContent.Create(new
        {
            From = _options.FromEmail,
            To = toEmail,
            Subject = subject,
            HtmlBody = htmlBody,
            TextBody = textBody,
            MessageStream = _options.MessageStream,
            Tag = "password-reset"
        });

        HttpResponseMessage res;
        try
        {
            res = await _http.SendAsync(req, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Postmark send failed for {Email}", toEmail);
            return (false, "Failed to reach Postmark.");
        }

        if (res.IsSuccessStatusCode)
            return (true, null);

        var body = await res.Content.ReadAsStringAsync(ct);
        _logger.LogWarning("Postmark rejected email ({Status}): {Body}", (int)res.StatusCode, body);
        return (false, $"Postmark error {(int)res.StatusCode}");
    }
}
