using System.Collections.Concurrent;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Identity;

namespace dorks_and_dice_site.Tests;

public sealed record SentAccountEmail(
    SiteMode SiteMode,
    string Recipient,
    string Subject,
    string HtmlBody,
    string TextBody);

public sealed class TestAccountEmailSender : IAccountEmailSender
{
    public ConcurrentQueue<SentAccountEmail> Messages { get; } = new();

    public Task SendAsync(
        SiteMode siteMode,
        string recipient,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Messages.Enqueue(new SentAccountEmail(siteMode, recipient, subject, htmlBody, textBody));
        return Task.CompletedTask;
    }
}
