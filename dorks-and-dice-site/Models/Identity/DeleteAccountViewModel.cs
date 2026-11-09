using System.ComponentModel.DataAnnotations;

namespace dorks_and_dice_site.Models.Identity;

/// <summary>
/// Password confirmation before shared-account deletion. The controller
/// preserves the account tombstone and rejects privileged owner deletion.
/// </summary>
public sealed class DeleteAccountViewModel
{
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;
}
