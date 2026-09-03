using System.ComponentModel.DataAnnotations;

namespace dorks_and_dice_site.Models.Identity;

public sealed class ResendConfirmationViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
