namespace dorks_and_dice_site.Services.Identity;

public sealed record AccountEmailSenderIdentity(
    string Domain,
    string DisplayName);

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
