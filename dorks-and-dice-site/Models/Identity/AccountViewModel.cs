using System.ComponentModel.DataAnnotations;

namespace dorks_and_dice_site.Models.Identity;

/// <summary>
/// Shared account settings projection, including both global external links
/// and their separate active normal-mode connections.
/// </summary>
public sealed class AccountViewModel
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public bool IsAdministrator { get; init; }
    public bool IsDeveloper { get; init; }
    public bool HasTrustedAccess { get; init; }
    public IReadOnlyList<AccountLinkViewModel> AccountLinks { get; init; } = [];

    [Required]
    [StringLength(ApplicationUser.DisplayNameMaxLength, MinimumLength = 1)]
    [Display(Name = "Display name")]
    public string DisplayName { get; set; } = string.Empty;
}


public sealed record AccountLinkViewModel(
    string ProviderId,
    string DisplayName,
    bool IsGloballyLinked,
    bool IsActiveForMode,
    string? ExternalDisplayName);
