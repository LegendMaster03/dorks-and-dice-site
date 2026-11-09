using Microsoft.AspNetCore.Identity;

namespace dorks_and_dice_site.Models.Identity;

/// <summary>
/// Shared Identity account for either hosted site. AccountKind distinguishes
/// humans from service principals, while mode-specific roles and external
/// workspace activation are stored separately from this record.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public const int DisplayNameMaxLength = 80;

    public AccountKind AccountKind { get; set; } = AccountKind.Human;
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
}
