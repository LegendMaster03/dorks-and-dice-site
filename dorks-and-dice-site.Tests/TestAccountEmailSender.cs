using System.Collections.Concurrent;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Tests;

public sealed record SentAccountEmail(
    AccountEmailSenderIdentity SenderIdentity,
    string Recipient,
    string Subject,
    string HtmlBody,
    string TextBody)
{
    // Temporary compatibility projection for the older integration assertion. Production
    // email transport no longer carries the SiteMode enum.
    public SiteMode SiteMode
    {
        get
        {
            var modeId = new SiteModeOptions().ResolveModeId(SenderIdentity.Domain);
            return modeId is not null
                && BuiltInSiteModes.All.FirstOrDefault(mode =>
                    string.Equals(mode.Id, modeId, StringComparison.OrdinalIgnoreCase))?.LegacyMode is { } legacyMode
                    ? legacyMode
                    : SiteMode.Unassigned;
        }
    }
}

public sealed class TestAccountEmailSender : IAccountEmailSender
{
    public ConcurrentQueue<SentAccountEmail> Messages { get; } = new();

    public Task SendAsync(
        AccountEmailSenderIdentity senderIdentity,
        string recipient,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Messages.Enqueue(new SentAccountEmail(senderIdentity, recipient, subject, htmlBody, textBody));
        return Task.CompletedTask;
    }
}
