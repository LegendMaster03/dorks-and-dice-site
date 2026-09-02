using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Identity;

public interface IAccountEmailSender
{
    Task SendAsync(
        SiteMode siteMode,
        string recipient,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default);
}
