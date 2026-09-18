using System.ComponentModel.DataAnnotations;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;

namespace dorks_and_dice_site.Models.Operator;

public sealed class AdminAgentListViewModel
{
    public IReadOnlyList<AdminAgentListItemViewModel> Agents { get; init; } = [];
}

public sealed class AdminAgentListItemViewModel
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? DeletedAt { get; init; }
    public IReadOnlyList<string> GlobalRoles { get; init; } = [];
    public int ActiveCredentialCount { get; init; }
}

public sealed class AdminAgentCreateViewModel
{
    [Required]
    [StringLength(ApplicationUser.DisplayNameMaxLength)]
    public string DisplayName { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = [AccountRoles.GlobalEditor, AccountRoles.RulesLawyer];

    [Required]
    [StringLength(OperatorCredential.NameMaxLength)]
    public string CredentialName { get; set; } = "initial";

    public DateTimeOffset? ExpiresAt { get; set; }

    public IReadOnlyList<string> AvailableRoles { get; init; } = AccountRoles.UiAssignable;
}

public sealed class AdminAgentCredentialCreateViewModel
{
    [Required]
    [StringLength(OperatorCredential.NameMaxLength)]
    public string CredentialName { get; set; } = "rotated";

    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class AdminAgentCredentialViewModel
{
    public Guid CredentialId { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }
    public DateTimeOffset? LastUsedAt { get; init; }

    public bool IsActive =>
        !RevokedAt.HasValue
        && (!ExpiresAt.HasValue || ExpiresAt > DateTimeOffset.UtcNow);
}

public sealed class AdminAgentDetailViewModel
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? DeletedAt { get; init; }
    public IReadOnlyList<string> GlobalRoles { get; init; } = [];
    public IReadOnlyList<AdminAgentCredentialViewModel> Credentials { get; init; } = [];
    public AdminAgentCredentialCreateViewModel NewCredential { get; init; } = new();
}

public sealed class AdminAgentCredentialCreatedViewModel
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public Guid CredentialId { get; init; }
    public string CredentialName { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; init; }
    public bool NewAgent { get; init; }
}
