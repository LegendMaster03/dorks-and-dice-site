using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Operator;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Operator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Controllers;

[Authorize(Policy = AuthorizationPolicies.OwnerAccess)]
[Route("admin/agents")]
public sealed class AdminAgentsController(
    UserManager<ApplicationUser> userManager,
    IOperatorPrincipalService principals,
    IOperatorCredentialService credentials) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var users = await userManager.Users
            .Where(user => user.AccountKind == AccountKind.ServicePrincipal)
            .OrderByDescending(user => user.CreatedAt)
            .ToArrayAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var agents = new List<AdminAgentListItemViewModel>(users.Length);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            var userCredentials = await credentials.GetForUserAsync(user.Id, cancellationToken);
            agents.Add(new AdminAgentListItemViewModel
            {
                UserId = user.Id,
                DisplayName = user.DisplayName,
                CreatedAt = user.CreatedAt,
                DeletedAt = user.DeletedAt,
                GlobalRoles = roles.OrderBy(role => role, StringComparer.Ordinal).ToArray(),
                ActiveCredentialCount = userCredentials.Count(credential =>
                    credential.RevokedAt is null
                    && (!credential.ExpiresAt.HasValue || credential.ExpiresAt > now))
            });
        }

        return View(new AdminAgentListViewModel { Agents = agents });
    }

    [HttpGet("create")]
    public IActionResult Create() => View(new AdminAgentCreateViewModel());

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        AdminAgentCreateViewModel model,
        CancellationToken cancellationToken)
    {
        model.Roles = model.Roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (model.Roles.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Roles), "Select at least one role.");
        }

        var invalidRoles = model.Roles
            .Where(role => !AccountRoles.UiAssignable.Contains(role, StringComparer.Ordinal))
            .ToArray();
        if (invalidRoles.Length > 0)
        {
            ModelState.AddModelError(nameof(model.Roles), "One or more selected roles are not assignable.");
        }

        if (model.ExpiresAt.HasValue && model.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            ModelState.AddModelError(nameof(model.ExpiresAt), "Credential expiry must be in the future.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var created = await principals.CreateAsync(
                model.DisplayName,
                model.Roles,
                model.CredentialName,
                model.ExpiresAt,
                cancellationToken);
            return View("CredentialCreated", ToCreatedViewModel(
                created.User,
                created.Credential,
                newAgent: true));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> Details(Guid userId, CancellationToken cancellationToken)
    {
        var user = await FindAgentAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        return View(await BuildDetailAsync(user, cancellationToken));
    }

    [HttpPost("{userId:guid}/credentials")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCredential(
        Guid userId,
        AdminAgentCredentialCreateViewModel model,
        CancellationToken cancellationToken)
    {
        var user = await FindAgentAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        if (user.DeletedAt is not null)
        {
            return BadRequest("Deleted service principals can not receive new credentials.");
        }

        if (model.ExpiresAt.HasValue && model.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            ModelState.AddModelError(nameof(model.ExpiresAt), "Credential expiry must be in the future.");
        }

        if (!ModelState.IsValid)
        {
            return View("Details", await BuildDetailAsync(user, cancellationToken, model));
        }

        try
        {
            var created = await credentials.CreateAsync(
                user.Id,
                model.CredentialName,
                model.ExpiresAt,
                cancellationToken);
            return View("CredentialCreated", ToCreatedViewModel(user, created, newAgent: false));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("Details", await BuildDetailAsync(user, cancellationToken, model));
        }
    }

    [HttpPost("{userId:guid}/credentials/{credentialId:guid}/revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeCredential(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken)
    {
        var user = await FindAgentAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        if (!await credentials.RevokeForUserAsync(userId, credentialId, cancellationToken))
        {
            return NotFound();
        }

        TempData["AdminAgentMessage"] = "Credential revoked. Browser sessions bound to it will be rejected.";
        return RedirectToAction(nameof(Details), new { userId });
    }

    private async Task<ApplicationUser?> FindAgentAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString("D"));
        return user?.AccountKind == AccountKind.ServicePrincipal ? user : null;
    }

    private async Task<AdminAgentDetailViewModel> BuildDetailAsync(
        ApplicationUser user,
        CancellationToken cancellationToken,
        AdminAgentCredentialCreateViewModel? newCredential = null)
    {
        var roles = await userManager.GetRolesAsync(user);
        var userCredentials = await credentials.GetForUserAsync(user.Id, cancellationToken);
        return new AdminAgentDetailViewModel
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            CreatedAt = user.CreatedAt,
            DeletedAt = user.DeletedAt,
            GlobalRoles = roles.OrderBy(role => role, StringComparer.Ordinal).ToArray(),
            Credentials = userCredentials.Select(credential => new AdminAgentCredentialViewModel
            {
                CredentialId = credential.Id,
                Name = credential.Name,
                CreatedAt = credential.CreatedAt,
                ExpiresAt = credential.ExpiresAt,
                RevokedAt = credential.RevokedAt,
                LastUsedAt = credential.LastUsedAt
            }).ToArray(),
            NewCredential = newCredential ?? new AdminAgentCredentialCreateViewModel()
        };
    }

    private static AdminAgentCredentialCreatedViewModel ToCreatedViewModel(
        ApplicationUser user,
        OperatorCredentialCreationResult credential,
        bool newAgent) => new()
    {
        UserId = user.Id,
        DisplayName = user.DisplayName,
        CredentialId = credential.Credential.Id,
        CredentialName = credential.Credential.Name,
        Token = credential.Token,
        ExpiresAt = credential.Credential.ExpiresAt,
        NewAgent = newAgent
    };
}
