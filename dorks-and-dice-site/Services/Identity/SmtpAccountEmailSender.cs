using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Options;

namespace dorks_and_dice_site.Services.Identity;

public sealed class SmtpAccountEmailSender : IAccountEmailSender
{
    private readonly AccountEmailOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SmtpAccountEmailSender> _logger;

    public SmtpAccountEmailSender(
        IOptions<AccountEmailOptions> options,
        IWebHostEnvironment environment,
        ILogger<SmtpAccountEmailSender> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task SendAsync(
        AccountEmailSenderIdentity senderIdentity,
        string recipient,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(senderIdentity);

        var senderAddress = GetSenderAddress(senderIdentity.Domain);
        var senderDisplayName = senderIdentity.DisplayName.Trim();
        if (senderDisplayName.Length == 0)
        {
            throw new InvalidOperationException("Account email sender display name is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogWarning(
                    "Account email transport is not configured. Development email from {Sender} to {Recipient}. Subject: {Subject}{NewLine}{Body}",
                    senderAddress,
                    recipient,
                    subject,
                    Environment.NewLine,
                    textBody);
                return;
            }

            throw new InvalidOperationException("Account email SMTP transport is not configured.");
        }

        var password = ResolvePassword();
        using var message = new MailMessage
        {
            From = new MailAddress(senderAddress, senderDisplayName),
            Subject = subject
        };
        message.To.Add(new MailAddress(recipient));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(textBody, null, MediaTypeNames.Text.Plain));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, null, MediaTypeNames.Text.Html));

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.EnableSsl,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, password);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private string GetSenderAddress(string domain)
    {
        var localPart = string.IsNullOrWhiteSpace(_options.FromLocalPart)
            ? "accounts"
            : _options.FromLocalPart.Trim();
        var normalizedDomain = domain.Trim();
        if (normalizedDomain.Length == 0)
        {
            throw new InvalidOperationException("Account email sender domain is required.");
        }

        return $"{localPart}@{normalizedDomain}";
    }

    private string? ResolvePassword()
    {
        if (!string.IsNullOrWhiteSpace(_options.Password))
        {
            return _options.Password;
        }

        if (string.IsNullOrWhiteSpace(_options.PasswordFile))
        {
            return null;
        }

        if (!File.Exists(_options.PasswordFile))
        {
            throw new InvalidOperationException($"Account email password file '{_options.PasswordFile}' does not exist.");
        }

        return File.ReadAllText(_options.PasswordFile).Trim();
    }
}
