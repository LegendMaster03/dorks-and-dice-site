using System.Net;
using System.Security.Cryptography;
using System.Text;
using dorks_and_dice_site.Models.Identity;
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
    private readonly ISiteModePresentationService _siteModePresentationService;
    private readonly IAccountEmailSender _emailSender;
    private readonly IAccountLinkProviderCatalog _accountLinkProviders;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        SiteModeOptions siteModeOptions,
        ISiteModePresentationService siteModePresentationService,
        IAccountEmailSender emailSender,
        IAccountLinkProviderCatalog accountLinkProviders,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _siteModeOptions = siteModeOptions;
        _siteModePresentationService = siteModePresentationService;
        _emailSender = emailSender;
        _accountLinkProviders = accountLinkProviders;
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
    [HttpPost("links/{providerId}/connect")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> ConnectAccountLink(string providerId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null || user.DeletedAt is not null)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        if (!_accountLinkProviders.TryGet(providerId, out var provider))
        {
            return NotFound();
        }

        var existingLogins = await _userManager.GetLoginsAsync(user);
        if (existingLogins.Any(login =>
            string.Equals(login.LoginProvider, provider.Descriptor.Id, StringComparison.OrdinalIgnoreCase)))
        {
            TempData["AccountError"] = $"{provider.Descriptor.DisplayName} is already connected.";
            return RedirectToAction(nameof(Index));
        }

        var nonce = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var nonceResult = await _userManager.SetAuthenticationTokenAsync(
            user,
            AccountLinkTokenNames.LoginProvider,
            AccountLinkTokenNames.Nonce(provider.Descriptor.Id),
            nonce);
        if (!nonceResult.Succeeded)
        {
            TempData["AccountError"] = "The account-link request could not be started.";
            return RedirectToAction(nameof(Index));
        }

        var callbackPath = Url.Action(
            nameof(AccountLinkCallback),
            "Account",
            new { providerId = provider.Descriptor.Id })
            ?? $"/account/links/callback/{provider.Descriptor.Id}";
        var properties = provider.CreateChallengeProperties(user.Id, nonce, callbackPath);
        properties.Items[AccountLinkAuthenticationProperties.ProviderId] = provider.Descriptor.Id;

        return Challenge(properties, provider.Descriptor.AuthenticationScheme);
    }

    [Authorize]
    [AcceptVerbs("GET", "POST")]
    [Route("links/callback/{providerId}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AccountLinkCallback(string providerId)
    {
        if (!_accountLinkProviders.TryGet(providerId, out var provider))
        {
            return NotFound();
        }

        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null || user.DeletedAt is not null)
            {
                await _signInManager.SignOutAsync();
                return RedirectToAction(nameof(Login));
            }

            var external = await provider.AuthenticateAsync(HttpContext);
            if (external is null)
            {
                TempData["AccountError"] =
                    $"{provider.Descriptor.DisplayName} did not return a usable account identity.";
                return RedirectToAction(nameof(Index));
            }

            if (!external.Properties.Items.TryGetValue(
                    AccountLinkAuthenticationProperties.ProviderId,
                    out var protectedProviderId)
                || !string.Equals(
                    protectedProviderId,
                    provider.Descriptor.Id,
                    StringComparison.OrdinalIgnoreCase)
                || !external.Properties.Items.TryGetValue(
                    AccountLinkAuthenticationProperties.UserId,
                    out var protectedUserId)
                || !Guid.TryParse(protectedUserId, out var expectedUserId)
                || expectedUserId != user.Id
                || !external.Properties.Items.TryGetValue(
                    AccountLinkAuthenticationProperties.Nonce,
                    out var returnedNonce)
                || string.IsNullOrWhiteSpace(returnedNonce))
            {
                TempData["AccountError"] = "The account-link response could not be verified.";
                return RedirectToAction(nameof(Index));
            }

            var nonceName = AccountLinkTokenNames.Nonce(provider.Descriptor.Id);
            var pendingNonce = await _userManager.GetAuthenticationTokenAsync(
                user,
                AccountLinkTokenNames.LoginProvider,
                nonceName);
            if (!SecureEquals(pendingNonce, returnedNonce))
            {
                TempData["AccountError"] = "The account-link request is no longer valid.";
                return RedirectToAction(nameof(Index));
            }

            var consumeResult = await _userManager.RemoveAuthenticationTokenAsync(
                user,
                AccountLinkTokenNames.LoginProvider,
                nonceName);
            if (!consumeResult.Succeeded)
            {
                TempData["AccountError"] = "The account-link request could not be completed.";
                return RedirectToAction(nameof(Index));
            }

            var existingLogins = await _userManager.GetLoginsAsync(user);
            if (existingLogins.Any(login =>
                string.Equals(login.LoginProvider, provider.Descriptor.Id, StringComparison.OrdinalIgnoreCase)))
            {
                TempData["AccountError"] =
                    $"{provider.Descriptor.DisplayName} is already connected.";
                return RedirectToAction(nameof(Index));
            }

            var otherUser = await _userManager.FindByLoginAsync(
                provider.Descriptor.Id,
                external.Identity.ProviderKey);
            if (otherUser is not null && otherUser.Id != user.Id)
            {
                TempData["AccountError"] =
                    $"That {provider.Descriptor.DisplayName} account is already connected to another account.";
                return RedirectToAction(nameof(Index));
            }

            var addResult = await _userManager.AddLoginAsync(
                user,
                new UserLoginInfo(
                    provider.Descriptor.Id,
                    external.Identity.ProviderKey,
                    provider.Descriptor.DisplayName));
            if (!addResult.Succeeded)
            {
                TempData["AccountError"] = string.Join(
                    " ",
                    addResult.Errors.Select(error => error.Description));
                return RedirectToAction(nameof(Index));
            }

            if (!string.IsNullOrWhiteSpace(external.Identity.DisplayName))
            {
                var displayNameResult = await _userManager.SetAuthenticationTokenAsync(
                    user,
                    provider.Descriptor.Id,
                    AccountLinkTokenNames.ExternalDisplayName,
                    external.Identity.DisplayName);
                if (!displayNameResult.Succeeded)
                {
                    _logger.LogWarning(
                        "Linked provider {ProviderId} for user {UserId}, but could not persist its display name.",
                        provider.Descriptor.Id,
                        user.Id);
                }
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["AccountMessage"] = $"{provider.Descriptor.DisplayName} connected.";
            return RedirectToAction(nameof(Index));
        }
        finally
        {
            await provider.CleanupAsync(HttpContext);
        }
    }

    [Authorize]
    [HttpPost("links/{providerId}/disconnect")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisconnectAccountLink(string providerId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null || user.DeletedAt is not null)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        if (!_accountLinkProviders.TryGet(providerId, out var provider))
        {
            return NotFound();
        }

        var login = (await _userManager.GetLoginsAsync(user))
            .SingleOrDefault(candidate =>
                string.Equals(
                    candidate.LoginProvider,
                    provider.Descriptor.Id,
                    StringComparison.OrdinalIgnoreCase));
        if (login is null)
        {
            TempData["AccountError"] = $"{provider.Descriptor.DisplayName} is not connected.";
            return RedirectToAction(nameof(Index));
        }

        var removeResult = await _userManager.RemoveLoginAsync(
            user,
            login.LoginProvider,
            login.ProviderKey);
        if (!removeResult.Succeeded)
        {
            TempData["AccountError"] = string.Join(
                " ",
                removeResult.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Index));
        }

        await _userManager.RemoveAuthenticationTokenAsync(
            user,
            provider.Descriptor.Id,
            AccountLinkTokenNames.ExternalDisplayName);
        await _userManager.RemoveAuthenticationTokenAsync(
            user,
            AccountLinkTokenNames.LoginProvider,
            AccountLinkTokenNames.Nonce(provider.Descriptor.Id));

        await _signInManager.RefreshSignInAsync(user);
        TempData["AccountMessage"] = $"{provider.Descriptor.DisplayName} disconnected.";
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

        if (await _userManager.IsInRoleAsync(user, AccountRoles.Owner))
        {
            ModelState.AddModelError(
                string.Empty,
                "Owner accounts can not be deleted through the UI. Remove the Owner role server-side first.");
            return View(model);
        }

        if (await IsLastActiveAdministratorAsync(user))
        {
            ModelState.AddModelError(
                string.Empty,
                "The final active administrator account can not be deleted. Assign Admin to another active account first.");
            return View(model);
        }

        foreach (var login in await _userManager.GetLoginsAsync(user))
        {
            var removeLoginResult = await _userManager.RemoveLoginAsync(
                user,
                login.LoginProvider,
                login.ProviderKey);
            if (!removeLoginResult.Succeeded)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Linked accounts could not be removed. The account was not deleted.");
                return View(model);
            }

            await _userManager.RemoveAuthenticationTokenAsync(
                user,
                login.LoginProvider,
                AccountLinkTokenNames.ExternalDisplayName);
        }

        foreach (var provider in _accountLinkProviders.All)
        {
            await _userManager.RemoveAuthenticationTokenAsync(
                user,
                AccountLinkTokenNames.LoginProvider,
                AccountLinkTokenNames.Nonce(provider.Descriptor.Id));
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
        var isOwner = await _userManager.IsInRoleAsync(user, AccountRoles.Owner);
        var isAdministrator = isOwner || await _userManager.IsInRoleAsync(user, AccountRoles.Admin);
        var isDeveloper = isOwner || await _userManager.IsInRoleAsync(user, AccountRoles.Dev);
        var hasTrustedAccess = TrustedAccessEvaluator.IsAuthorized(HttpContext, _siteModeOptions);
        var accountLinks = await BuildAccountLinksAsync(user);

        return new AccountViewModel
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            DisplayName = displayName ?? user.DisplayName,
            IsAdministrator = isAdministrator,
            IsDeveloper = isDeveloper,
            HasTrustedAccess = hasTrustedAccess,
            AccountLinks = accountLinks
        };
    }

    private async Task<IReadOnlyList<AccountLinkViewModel>> BuildAccountLinksAsync(
        ApplicationUser user)
    {
        var existingLogins = await _userManager.GetLoginsAsync(user);
        var byProvider = existingLogins.ToDictionary(
            login => login.LoginProvider,
            StringComparer.OrdinalIgnoreCase);
        var links = new List<AccountLinkViewModel>(_accountLinkProviders.All.Count);

        foreach (var provider in _accountLinkProviders.All)
        {
            if (!byProvider.TryGetValue(provider.Descriptor.Id, out _))
            {
                links.Add(new AccountLinkViewModel(
                    provider.Descriptor.Id,
                    provider.Descriptor.DisplayName,
                    IsLinked: false,
                    ExternalDisplayName: null));
                continue;
            }

            var externalDisplayName = await _userManager.GetAuthenticationTokenAsync(
                user,
                provider.Descriptor.Id,
                AccountLinkTokenNames.ExternalDisplayName);
            links.Add(new AccountLinkViewModel(
                provider.Descriptor.Id,
                provider.Descriptor.DisplayName,
                IsLinked: true,
                ExternalDisplayName: externalDisplayName));
        }

        return links;
    }

    private static bool SecureEquals(string? left, string? right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
        {
            return false;
        }

        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

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

        var modeContext = HttpContext.GetSiteModeContext();
        var modeId = modeContext.ActiveModeId
            ?? throw new InvalidOperationException("Account email requires an active hosted site mode.");
        if (!_siteModeOptions.TryGetCanonicalHost(modeId, out var senderDomain))
        {
            throw new InvalidOperationException($"No canonical host is configured for site mode '{modeId}'.");
        }

        var siteName = _siteModePresentationService.GetTitleSuffix(modeContext);
        var senderIdentity = new AccountEmailSenderIdentity(senderDomain!, siteName);
        var encodedUrl = WebUtility.HtmlEncode(confirmationUrl);
        var htmlBody = $"<p>Confirm your email address for your {WebUtility.HtmlEncode(siteName)} account.</p>"
            + $"<p><a href=\"{encodedUrl}\">Confirm email</a></p>"
            + "<p>This link expires in 24 hours.</p>";
        var textBody = $"Confirm your email address for your {siteName} account:\n\n{confirmationUrl}\n\n"
            + "This link expires in 24 hours.";

        await _emailSender.SendAsync(
            senderIdentity,
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
