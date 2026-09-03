namespace dorks_and_dice_site.Models.Identity;

public sealed class AdminAccountListViewModel
{
    public List<AdminAccountListItemViewModel> Accounts { get; init; } = [];
}

public sealed class AdminAccountListItemViewModel
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool EmailConfirmed { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? DeletedAt { get; init; }
    public DateTimeOffset? LockoutEnd { get; init; }
    public List<string> GlobalRoles { get; init; } = [];
}

public sealed class AdminAccountDetailViewModel
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool EmailConfirmed { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? DeletedAt { get; init; }
    public DateTimeOffset? LockoutEnd { get; init; }
    public bool IsCurrentUser { get; init; }
    public List<string> GlobalRoles { get; init; } = [];
    public Dictionary<string, List<string>> ScopedRoles { get; init; } = new(StringComparer.Ordinal);
}
