using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Controllers;

[Authorize(Policy = AuthorizationPolicies.AdminAccess)]
[Route("admin/accounts")]
public sealed class AdminAccountsController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly IScopedRoleService _scopedRoleService;

    public AdminAccountsController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IScopedRoleService scopedRoleService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _scopedRoleService = scopedRoleService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        // SQLite can not translate ORDER BY for DateTimeOffset. Materialize first so
        // local account management uses the same ordering without provider-specific SQL.
        var users = (await _userManager.Users.ToListAsync())
            .OrderByDescending(user => user.CreatedAt)
            .ToList();
        var accounts = new List<AdminAccountListItemViewModel>(users.Count);

        foreach (var user in users)
        {
            accounts.Add(new AdminAccountListItemViewModel
            {
                UserId = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email ?? string.Empty,
                EmailConfirmed = user.EmailConfirmed,
                CreatedAt = user.CreatedAt,
                DeletedAt = user.DeletedAt,
                LockoutEnd = user.LockoutEnd,
                GlobalRoles = (await _userManager.GetRolesAsync(user)).OrderBy(role => role).ToList()
            });
        }

        return View(new AdminAccountListViewModel { Accounts = accounts });
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> Details(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return NotFound();
        }

        return View(await BuildDetailsAsync(user));
    }

    [HttpPost("{userId:guid}/global-role")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetGlobalRole(Guid userId, string role, bool enabled)
    {
        if (!AccountRoles.Privileged.Contains(role, StringComparer.Ordinal))
        {
            return BadRequest();
        }

        // Admin may manage accounts and scoped Editor access, but only Owner may
        // delegate or revoke the global Admin and Dev roles.
        if (!User.IsInRole(AccountRoles.Owner))
        {
            return Forbid();
        }

        var user = await FindMutableUserAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        if (await _userManager.IsInRoleAsync(user, AccountRoles.Owner))
        {
            TempData["AdminAccountError"] = "Owner accounts inherit Admin and Dev. Their global role assignment is server-managed.";
            return RedirectToAction(nameof(Details), new { userId });
        }

        var currentlyEnabled = await _userManager.IsInRoleAsync(user, role);
        if (currentlyEnabled == enabled)
        {
            return RedirectToAction(nameof(Details), new { userId });
        }

        if (!enabled
            && string.Equals(role, AccountRoles.Admin, StringComparison.Ordinal)
            && await IsLastActiveAdministratorAsync(user))
        {
            TempData["AdminAccountError"] = "The final active administrator role can not be removed.";
            return RedirectToAction(nameof(Details), new { userId });
        }

        if (enabled && !await EnsureRoleExistsAsync(role))
        {
            TempData["AdminAccountError"] = $"The {role} role could not be initialized.";
            return RedirectToAction(nameof(Details), new { userId });
        }

        var result = enabled
            ? await _userManager.AddToRoleAsync(user, role)
            : await _userManager.RemoveFromRoleAsync(user, role);
        if (!result.Succeeded)
        {
            TempData["AdminAccountError"] = string.Join(" ", result.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Details), new { userId });
        }

        await _userManager.UpdateSecurityStampAsync(user);
        if (IsCurrentUser(user))
        {
            await _signInManager.RefreshSignInAsync(user);
        }

        TempData["AdminAccountMessage"] = enabled
            ? $"{role} role assigned."
            : $"{role} role removed.";

        if (IsCurrentUser(user)
            && !enabled
            && string.Equals(role, AccountRoles.Admin, StringComparison.Ordinal))
        {
            return RedirectToAction("Index", "Account");
        }

        return RedirectToAction(nameof(Details), new { userId });
    }

    [HttpPost("{userId:guid}/scoped-role")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetScopedRole(
        Guid userId,
        string scope,
        string role,
        bool enabled)
    {
        if (!AccountRoleScopes.All.Contains(scope, StringComparer.Ordinal)
            || !ScopedAccountRoles.All.Contains(role, StringComparer.Ordinal))
        {
            return BadRequest();
        }

        var user = await FindMutableUserAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        var result = await _scopedRoleService.SetRoleAsync(user, scope, role, enabled);
        if (!result.Succeeded)
        {
            TempData["AdminAccountError"] = string.Join(" ", result.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Details), new { userId });
        }

        if (IsCurrentUser(user))
        {
            await _signInManager.RefreshSignInAsync(user);
        }

        TempData["AdminAccountMessage"] = enabled
            ? $"{scope} {role} role assigned."
            : $"{scope} {role} role removed.";
        return RedirectToAction(nameof(Details), new { userId });
    }

    [HttpPost("{userId:guid}/lock")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lock(Guid userId)
    {
        var user = await FindMutableUserAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        if (await IsOwnerTargetRestrictedAsync(user))
        {
            return Forbid();
        }

        if (IsCurrentUser(user))
        {
            TempData["AdminAccountError"] = "The current administrator can not lock its own account.";
            return RedirectToAction(nameof(Details), new { userId });
        }

        if (await IsLastActiveAdministratorAsync(user))
        {
            TempData["AdminAccountError"] = "The final active administrator account can not be locked.";
            return RedirectToAction(nameof(Details), new { userId });
        }

        var enableResult = await _userManager.SetLockoutEnabledAsync(user, true);
        var lockResult = enableResult.Succeeded
            ? await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue)
            : enableResult;
        if (!lockResult.Succeeded)
        {
            TempData["AdminAccountError"] = string.Join(" ", lockResult.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Details), new { userId });
        }

        await _userManager.UpdateSecurityStampAsync(user);
        TempData["AdminAccountMessage"] = "Account locked and existing sessions invalidated.";
        return RedirectToAction(nameof(Details), new { userId });
    }

    [HttpPost("{userId:guid}/unlock")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(Guid userId)
    {
        var user = await FindMutableUserAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        if (await IsOwnerTargetRestrictedAsync(user))
        {
            return Forbid();
        }

        var result = await _userManager.SetLockoutEndDateAsync(user, null);
        if (!result.Succeeded)
        {
            TempData["AdminAccountError"] = string.Join(" ", result.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Details), new { userId });
        }

        TempData["AdminAccountMessage"] = "Account unlocked.";
        return RedirectToAction(nameof(Details), new { userId });
    }

    [HttpPost("{userId:guid}/invalidate-sessions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InvalidateSessions(Guid userId)
    {
        var user = await FindMutableUserAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        if (await IsOwnerTargetRestrictedAsync(user))
        {
            return Forbid();
        }

        var result = await _userManager.UpdateSecurityStampAsync(user);
        if (!result.Succeeded)
        {
            TempData["AdminAccountError"] = string.Join(" ", result.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Details), new { userId });
        }

        TempData["AdminAccountMessage"] = "Existing sessions invalidated.";
        return IsCurrentUser(user)
            ? RedirectToAction("Login", "Account")
            : RedirectToAction(nameof(Details), new { userId });
    }

    private async Task<AdminAccountDetailViewModel> BuildDetailsAsync(ApplicationUser user)
    {
        var scopedRoles = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var scope in AccountRoleScopes.All)
        {
            scopedRoles[scope] = (await _scopedRoleService.GetRolesAsync(user, scope))
                .OrderBy(role => role)
                .ToList();
        }

        return new AdminAccountDetailViewModel
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            CreatedAt = user.CreatedAt,
            DeletedAt = user.DeletedAt,
            LockoutEnd = user.LockoutEnd,
            IsCurrentUser = IsCurrentUser(user),
            GlobalRoles = (await _userManager.GetRolesAsync(user)).OrderBy(role => role).ToList(),
            ScopedRoles = scopedRoles
        };
    }

    private async Task<ApplicationUser?> FindMutableUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user?.DeletedAt is null ? user : null;
    }

    private bool IsCurrentUser(ApplicationUser user) =>
        _userManager.GetUserId(User) == user.Id.ToString();

    private async Task<bool> IsOwnerTargetRestrictedAsync(ApplicationUser user) =>
        await _userManager.IsInRoleAsync(user, AccountRoles.Owner)
        && !User.IsInRole(AccountRoles.Owner);

    private async Task<bool> IsLastActiveAdministratorAsync(ApplicationUser user)
    {
        var isAdministrator = await _userManager.IsInRoleAsync(user, AccountRoles.Admin)
            || await _userManager.IsInRoleAsync(user, AccountRoles.Owner);
        if (!isAdministrator)
        {
            return false;
        }

        if (!await _signInManager.CanSignInAsync(user) || await _userManager.IsLockedOutAsync(user))
        {
            return false;
        }

        var candidates = new Dictionary<Guid, ApplicationUser>();
        foreach (var role in new[] { AccountRoles.Owner, AccountRoles.Admin })
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            foreach (var candidate in await _userManager.GetUsersInRoleAsync(role))
            {
                candidates[candidate.Id] = candidate;
            }
        }

        var activeAdministratorCount = 0;
        foreach (var candidate in candidates.Values)
        {
            if (candidate.DeletedAt is not null
                || !await _signInManager.CanSignInAsync(candidate)
                || await _userManager.IsLockedOutAsync(candidate))
            {
                continue;
            }

            activeAdministratorCount += 1;
            if (activeAdministratorCount > 1)
            {
                return false;
            }
        }

        return activeAdministratorCount <= 1;
    }

    private async Task<bool> EnsureRoleExistsAsync(string role)
    {
        if (await _roleManager.RoleExistsAsync(role))
        {
            return true;
        }

        var result = await _roleManager.CreateAsync(new IdentityRole<Guid>(role));
        return result.Succeeded || await _roleManager.RoleExistsAsync(role);
    }
}
