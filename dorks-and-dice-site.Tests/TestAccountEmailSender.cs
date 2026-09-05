using System.Collections.Concurrent;
using dorks_and_dice_site.Services.Identity;

namespace dorks_and_dice_site.Tests;

public sealed record SentAccountEmail(
    AccountEmailSenderIdentity SenderIdentity,
    string Recipient,
    string Subject,
    string HtmlBody,
    string TextBody);

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
