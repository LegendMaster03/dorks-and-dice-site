using System.ComponentModel.DataAnnotations;

namespace dorks_and_dice_site.Models.Identity;

public sealed class AccountViewModel
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(ApplicationUser.DisplayNameMaxLength, MinimumLength = 1)]
    [Display(Name = "Display name")]
    public string DisplayName { get; set; } = string.Empty;
}
