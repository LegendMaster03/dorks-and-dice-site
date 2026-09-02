using Microsoft.AspNetCore.Identity;

namespace dorks_and_dice_site.Models.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public const int DisplayNameMaxLength = 80;

    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
}
