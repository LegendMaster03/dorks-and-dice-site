using System.ComponentModel.DataAnnotations;

namespace dorks_and_dice_site.Models.Identity;

/// <summary>
/// Human account login form. The controller normalizes ReturnUrl to a local
/// redirect and delegates account eligibility and lockout to Identity.
/// </summary>
public sealed class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
