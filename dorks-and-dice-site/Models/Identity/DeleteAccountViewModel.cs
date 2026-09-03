using System.ComponentModel.DataAnnotations;

namespace dorks_and_dice_site.Models.Identity;

public sealed class DeleteAccountViewModel
{
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;
}
