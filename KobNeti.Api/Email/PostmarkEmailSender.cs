using PostmarkDotNet;

namespace KobNeti.Api.Email;

public class PostmarkOptions
{
    public const string SectionName = "Postmark";

    /// <summary>Server API token from Postmark.</summary>
    public string ServerToken { get; set; } = "";

    /// <summary>Verified sender, e.g. info@kobneti.com</summary>
    public string FromEmail { get; set; } = "info@kobneti.com";

    /// <summary>Transactional stream. Use outbound for invites; broadcast is for campaigns.</summary>
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
        CancellationToken ct = default,
        string tag = "ops");
}

public sealed class PostmarkEmailSender : IEmailSender
{
    private readonly PostmarkOptions _options;
    private readonly ILogger<PostmarkEmailSender> _logger;

    public PostmarkEmailSender(
        Microsoft.Extensions.Options.IOptions<PostmarkOptions> options,
        ILogger<PostmarkEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<(bool Ok, string? Error)> SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken ct = default,
        string tag = "ops")
    {
        if (!_options.IsConfigured)
            return (false, "Postmark is not configured (Postmark:ServerToken).");

        try
        {
            var message = new PostmarkMessage
            {
                To = toEmail.Trim(),
                From = string.IsNullOrWhiteSpace(_options.FromEmail) ? "info@kobneti.com" : _options.FromEmail.Trim(),
                TrackOpens = true,
                Subject = subject,
                TextBody = textBody,
                HtmlBody = htmlBody,
                MessageStream = string.IsNullOrWhiteSpace(_options.MessageStream) ? "outbound" : _options.MessageStream.Trim(),
                Tag = string.IsNullOrWhiteSpace(tag) ? "ops" : tag
            };

            var client = new PostmarkClient(_options.ServerToken.Trim());
            var sendResult = await client.SendMessageAsync(message);

            if (sendResult.Status == PostmarkStatus.Success)
                return (true, null);

            var detail = string.IsNullOrWhiteSpace(sendResult.Message)
                ? $"Postmark {sendResult.Status}"
                : sendResult.Message;
            _logger.LogWarning("Postmark rejected email to {Email}: {Error} (code {Code})",
                toEmail, detail, sendResult.ErrorCode);
            return (false, detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Postmark send failed for {Email}", toEmail);
            return (false, ex.Message);
        }
    }
}
