using System.Security.Claims;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Services.Operator;

public interface IOperatorContentAccessService
{
    string ResolveSource(string? sourceKey);
    bool CanEdit(ClaimsPrincipal principal, ContentItem item);
    bool CanEdit(ClaimsPrincipal principal, ContentAuthoringDocument document);
    void PrepareCreate(ClaimsPrincipal principal, ContentAuthoringDocument document);
    void PrepareUpdate(
        ClaimsPrincipal principal,
        ContentAuthoringDocument submitted,
        ContentAuthoringDocument current);
}

public sealed class OperatorContentAccessService(
    IContentSourceRegistry sourceRegistry,
    ISiteModeRegistry siteModeRegistry) : IOperatorContentAccessService
{
    public string ResolveSource(string? sourceKey)
    {
        var requested = string.IsNullOrWhiteSpace(sourceKey)
            ? sourceRegistry.AuthoringSourceKey
            : sourceKey.Trim();
        if (!string.Equals(requested, sourceRegistry.AuthoringSourceKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Operator content authoring is restricted to the configured authoring workspace. Published/global source promotion remains a deliberate human development operation.");
        }

        return sourceRegistry.AuthoringSourceKey;
    }

    public bool CanEdit(ClaimsPrincipal principal, ContentItem item) =>
        item.VisibleInModes.Count > 0
        && item.VisibleInModes.All(modeId => ContentAuthoringModeAccess.CanEditMode(principal, modeId));

    public bool CanEdit(ClaimsPrincipal principal, ContentAuthoringDocument document)
    {
        var modes = NormalizeModes(document.VisibleModesSelection.Count > 0
            ? document.VisibleModesSelection
            : ParseModes(document.VisibleModesText));
        return modes.Count > 0
            && modes.All(modeId => ContentAuthoringModeAccess.CanEditMode(principal, modeId));
    }

    public void PrepareCreate(ClaimsPrincipal principal, ContentAuthoringDocument document)
    {
        var modes = NormalizeModes(document.VisibleModesSelection.Count > 0
            ? document.VisibleModesSelection
            : ParseModes(document.VisibleModesText));
        ValidateRegisteredModes(modes);

        if (!CanSelectModes(principal))
        {
            if (modes.Count != 1 || !ContentAuthoringModeAccess.CanEditMode(principal, modes[0]))
            {
                throw new UnauthorizedAccessException(
                    "A scoped editor service principal may create content only in one mode for which it has Editor authority.");
            }
        }

        if (modes.Count == 0)
        {
            throw new InvalidOperationException("At least one visible mode is required.");
        }

        SetModes(document, modes);
    }

    public void PrepareUpdate(
        ClaimsPrincipal principal,
        ContentAuthoringDocument submitted,
        ContentAuthoringDocument current)
    {
        if (!CanEdit(principal, current))
        {
            throw new UnauthorizedAccessException("The service principal can not edit this content item.");
        }

        if (!CanSelectModes(principal))
        {
            ContentAuthoringModeAccess.PreserveExistingDocumentModes(submitted, current);
            return;
        }

        var modes = NormalizeModes(submitted.VisibleModesSelection.Count > 0
            ? submitted.VisibleModesSelection
            : ParseModes(submitted.VisibleModesText));
        ValidateRegisteredModes(modes);
        if (modes.Count == 0)
        {
            throw new InvalidOperationException("At least one visible mode is required.");
        }
        SetModes(submitted, modes);
    }

    private bool CanSelectModes(ClaimsPrincipal principal) =>
        AccountRoleHierarchy.PrincipalHasGlobalRole(principal, AccountRoles.GlobalEditor);

    private void ValidateRegisteredModes(IReadOnlyList<string> modes)
    {
        var unknown = modes.Where(modeId => !siteModeRegistry.TryGetById(modeId, out _)).ToArray();
        if (unknown.Length > 0)
        {
            throw new InvalidOperationException(
                $"Unknown site mode(s): {string.Join(", ", unknown)}.");
        }
    }

    private static List<string> ParseModes(string? value) =>
        (value ?? string.Empty)
            .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    private static List<string> NormalizeModes(IEnumerable<string> modes) =>
        modes
            .Where(mode => !string.IsNullOrWhiteSpace(mode))
            .Select(mode => mode.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static void SetModes(ContentAuthoringDocument document, IReadOnlyList<string> modes)
    {
        document.VisibleModesSelection = modes.ToList();
        document.VisibleModesText = string.Join(',', modes);
    }
}
