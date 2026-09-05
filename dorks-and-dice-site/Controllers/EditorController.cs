using dorks_and_dice_site.Models.Editor;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Authorize]
[Route("editor")]
public sealed class EditorController : Controller
{
    private readonly ISiteModeRegistry _siteModeRegistry;
    private readonly SiteModeOptions _siteModeOptions;

    public EditorController(
        ISiteModeRegistry siteModeRegistry,
        SiteModeOptions siteModeOptions)
    {
        _siteModeRegistry = siteModeRegistry;
        _siteModeOptions = siteModeOptions;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        var modeContext = HttpContext.GetSiteModeContext();
        var hasGlobalEditor = AccountRoleHierarchy.PrincipalHasGlobalRole(User, AccountRoles.GlobalEditor);
        var modes = _siteModeRegistry.All
            .Where(mode => hasGlobalEditor || HasScopedEditorRole(mode.Id))
            .Select(mode => new EditorModeOption
            {
                ModeId = mode.Id,
                DisplayName = mode.DisplayName,
                EditorHref = modeContext.HasTrustedAccess
                    ? null
                    : ResolvePublicEditorHref(modeContext.ActiveModeId, mode.Id)
            })
            .ToList();

        if (modes.Count == 0)
        {
            return Forbid();
        }

        return View(new EditorIndexViewModel
        {
            IsTrustedPreview = modeContext.HasTrustedAccess,
            Modes = modes
        });
    }

    private bool HasScopedEditorRole(string scope)
    {
        var expectedValue = $"{scope}:{ScopedAccountRoles.Editor}";
        return User.HasClaim(AccountClaimTypes.ScopedRole, expectedValue);
    }

    private string? ResolvePublicEditorHref(string? activeModeId, string targetModeId)
    {
        if (string.Equals(activeModeId, targetModeId, StringComparison.OrdinalIgnoreCase))
        {
            return "/editor/content";
        }

        return _siteModeOptions.TryGetCanonicalHost(targetModeId, out var canonicalHost)
            ? $"https://{canonicalHost}/editor/content"
            : null;
    }
}
