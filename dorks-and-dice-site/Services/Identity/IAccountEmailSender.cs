namespace dorks_and_dice_site.Services.Identity;

public sealed record AccountEmailSenderIdentity(
    string Domain,
    string DisplayName);

/// <summary>
/// Sends global account notifications using the sender identity selected for
/// the request's site mode. Transport credentials remain deployment-owned.
/// </summary>
public interface IAccountEmailSender
{
    Task SendAsync(
        AccountEmailSenderIdentity senderIdentity,
        string recipient,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default);
}
