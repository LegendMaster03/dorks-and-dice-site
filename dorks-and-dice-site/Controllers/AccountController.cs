using System.Net;
using System.Text;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace dorks_and_dice_site.Controllers;

[Route("account")]
public sealed class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly SiteModeOptions _siteModeOptions;
    private readonly IAccountEmailSender _emailSender;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        SiteModeOptions siteModeOptions,
        IAccountEmailSender emailSender,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _siteModeOptions = siteModeOptions;
        _emailSender = emailSender;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Index));
        }

        return View(new LoginViewModel { ReturnUrl = NormalizeReturnUrl(returnUrl) });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        model.ReturnUrl = NormalizeReturnUrl(model.ReturnUrl);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || user.DeletedAt is not null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        if (await _userManager.IsInRoleAsync(user, AccountRoles.Admin)
            && !DevelopmentAccessEvaluator.IsAuthorized(HttpContext, _siteModeOptions))
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return LocalRedirect(model.ReturnUrl ?? "/");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Too many failed login attempts. Try again later.");
        }
        else if (result.IsNotAllowed)
        {
            ModelState.AddModelError(
                string.Empty,
                "This account is not available for sign-in. If your email has not been confirmed, request a new confirmation email below.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
        }

        return View(model);
    }

    [AllowAnonymous]
    [HttpGet("register")]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Index));
        }

        return View(new RegisterViewModel());
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim();
        var displayName = model.DisplayName.Trim();
        if (displayName.Length == 0)
        {
            ModelState.AddModelError(nameof(model.DisplayName), "Display name is required.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = displayName,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        try
        {
            await SendConfirmationEmailAsync(user, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to send initial confirmation email for user {UserId}.", user.Id);
            await _userManager.DeleteAsync(user);
            ModelState.AddModelError(
                string.Empty,
                "We could not send the confirmation email. Please try again later.");
            return View(model);
        }

        return RedirectToAction(nameof(RegistrationPending));
    }

    [AllowAnonymous]
    [HttpGet("registration-pending")]
    public IActionResult RegistrationPending() => View();

    [AllowAnonymous]
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(Guid userId, string? code)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(code))
        {
            ViewData["EmailConfirmationSucceeded"] = false;
            return View();
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.DeletedAt is not null)
        {
            ViewData["EmailConfirmationSucceeded"] = false;
            return View();
        }

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            ViewData["EmailConfirmationSucceeded"] = false;
            return View();
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        ViewData["EmailConfirmationSucceeded"] = result.Succeeded;
        return View();
    }

    [AllowAnonymous]
    [HttpGet("resend-confirmation")]
    public IActionResult ResendConfirmation()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Index));
        }

        return View(new ResendConfirmationViewModel());
    }

    [AllowAnonymous]
    [HttpPost("resend-confirmation")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> ResendConfirmation(
        ResendConfirmationViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (user is not null && user.DeletedAt is null && !await _userManager.IsEmailConfirmedAsync(user))
        {
            try
            {
                await SendConfirmationEmailAsync(user, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to resend confirmation email for user {UserId}.", user.Id);
                ModelState.AddModelError(
                    string.Empty,
                    "We could not send the confirmation email. Please try again later.");
                return View(model);
            }
        }

        return View("ResendConfirmationSent");
    }

    [Authorize]
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null || user.DeletedAt is not null)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        return View(await BuildAccountViewModelAsync(user));
    }

    [Authorize]
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(AccountViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null || user.DeletedAt is not null)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        model = await BuildAccountViewModelAsync(user, model.DisplayName);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var displayName = model.DisplayName.Trim();
        if (displayName.Length == 0)
        {
            ModelState.AddModelError(nameof(model.DisplayName), "Display name is required.");
            return View(model);
        }

        user.DisplayName = displayName;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["AccountMessage"] = "Account updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize]
    [HttpPost("claim-admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClaimAdmin()
    {
        if (!DevelopmentAccessEvaluator.IsAuthorized(HttpContext, _siteModeOptions))
        {
            return Forbid();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null || user.DeletedAt is not null)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        if (await _userManager.IsInRoleAsync(user, AccountRoles.Admin))
        {
            TempData["AccountMessage"] = "This account already has administrator access.";
            return RedirectToAction(nameof(Index));
        }

        if (!await _roleManager.RoleExistsAsync(AccountRoles.Admin))
        {
            var createRoleResult = await _roleManager.CreateAsync(new IdentityRole<Guid>(AccountRoles.Admin));
            if (!createRoleResult.Succeeded && !await _roleManager.RoleExistsAsync(AccountRoles.Admin))
            {
                _logger.LogError(
                    "Could not create the {AdminRole} role: {Errors}",
                    AccountRoles.Admin,
                    string.Join(", ", createRoleResult.Errors.Select(error => error.Description)));
                TempData["AccountError"] = "Administrator access could not be initialized.";
                return RedirectToAction(nameof(Index));
            }
        }

        var activeAdministrators = await _userManager.GetUsersInRoleAsync(AccountRoles.Admin);
        if (activeAdministrators.Any(candidate => candidate.DeletedAt is null))
        {
            TempData["AccountError"] = "An active administrator account already exists.";
            return RedirectToAction(nameof(Index));
        }

        // Adding the role persists the user update as well, so changing the stamp here
        // invalidates any pre-administrator sessions at the same time the role is granted.
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        var addRoleResult = await _userManager.AddToRoleAsync(user, AccountRoles.Admin);
        if (!addRoleResult.Succeeded)
        {
            _logger.LogError(
                "Could not grant the {AdminRole} role to user {UserId}: {Errors}",
                AccountRoles.Admin,
                user.Id,
                string.Join(", ", addRoleResult.Errors.Select(error => error.Description)));
            TempData["AccountError"] = "Administrator access could not be granted.";
            return RedirectToAction(nameof(Index));
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["AccountMessage"] = "Administrator access granted. This account now requires trusted development access to authenticate.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize]
    [HttpGet("change-password")]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Authorize]
    [HttpPost("change-password")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null || user.DeletedAt is not null)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["AccountMessage"] = "Password changed.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize]
    [HttpGet("delete")]
    public IActionResult Delete() => View(new DeleteAccountViewModel());

    [Authorize]
    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> Delete(DeleteAccountViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null || user.DeletedAt is not null)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        if (!await _userManager.CheckPasswordAsync(user, model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "The password is incorrect.");
            return View(model);
        }

        var deletedAt = DateTimeOffset.UtcNow;
        var tombstoneIdentity = $"deleted-{user.Id:N}@deleted.invalid";
        user.DeletedAt = deletedAt;
        user.DisplayName = "Deleted account";
        user.Email = tombstoneIdentity;
        user.NormalizedEmail = _userManager.NormalizeEmail(tombstoneIdentity);
        user.EmailConfirmed = false;
        user.UserName = tombstoneIdentity;
        user.NormalizedUserName = _userManager.NormalizeName(tombstoneIdentity);
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.PasswordHash = null;
        user.TwoFactorEnabled = false;
        user.LockoutEnabled = true;
        user.LockoutEnd = deletedAt.AddYears(100);
        user.SecurityStamp = Guid.NewGuid().ToString("N");

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Deleted));
    }

    [AllowAnonymous]
    [HttpGet("deleted")]
    public IActionResult Deleted() => View();

    [Authorize]
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Redirect("/");
    }

    [AllowAnonymous]
    [HttpGet("access-denied")]
    public IActionResult AccessDenied() => View();

    private async Task<AccountViewModel> BuildAccountViewModelAsync(
        ApplicationUser user,
        string? displayName = null)
    {
        var isAdministrator = await _userManager.IsInRoleAsync(user, AccountRoles.Admin);
        var canClaimAdministrator = false;

        if (!isAdministrator && DevelopmentAccessEvaluator.IsAuthorized(HttpContext, _siteModeOptions))
        {
            var activeAdministratorExists = false;
            if (await _roleManager.RoleExistsAsync(AccountRoles.Admin))
            {
                var administrators = await _userManager.GetUsersInRoleAsync(AccountRoles.Admin);
                activeAdministratorExists = administrators.Any(candidate => candidate.DeletedAt is null);
            }

            canClaimAdministrator = !activeAdministratorExists;
        }

        return new AccountViewModel
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            DisplayName = displayName ?? user.DisplayName,
            IsAdministrator = isAdministrator,
            CanClaimAdministrator = canClaimAdministrator
        };
    }

    private async Task SendConfirmationEmailAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var email = user.Email
            ?? throw new InvalidOperationException("The account does not have an email address.");
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var confirmationUrl = Url.Action(
            nameof(ConfirmEmail),
            "Account",
            new { userId = user.Id, code },
            Request.Scheme)
            ?? throw new InvalidOperationException("Could not generate an email confirmation URL.");

        var siteMode = HttpContext.GetSiteModeContext().SiteMode;
        var siteName = siteMode == SiteMode.Professional ? "Kyle Barnett" : "Dorks & Dice";
        var encodedUrl = WebUtility.HtmlEncode(confirmationUrl);
        var htmlBody = $"<p>Confirm your email address for your {WebUtility.HtmlEncode(siteName)} account.</p>"
            + $"<p><a href=\"{encodedUrl}\">Confirm email</a></p>"
            + "<p>This link expires in 24 hours.</p>";
        var textBody = $"Confirm your email address for your {siteName} account:\n\n{confirmationUrl}\n\n"
            + "This link expires in 24 hours.";

        await _emailSender.SendAsync(
            siteMode,
            email,
            $"Confirm your {siteName} account",
            htmlBody,
            textBody,
            cancellationToken);
    }

    private string? NormalizeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : null;
}
