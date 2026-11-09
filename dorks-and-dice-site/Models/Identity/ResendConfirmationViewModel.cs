using System.ComponentModel.DataAnnotations;

namespace dorks_and_dice_site.Models.Identity;

/// <summary>
/// Requests a replacement global-account email confirmation message.
/// </summary>
public sealed class ResendConfirmationViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
