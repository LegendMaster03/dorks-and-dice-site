using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Framework.Fallback;

/// <summary>
/// Supplies the framework fallback homepage when no normal site mode is selected.
/// Unassigned is a framework state, not a third hosted site.
/// </summary>
public sealed class FallbackHomeModule : ISiteModeHomeModule
{
    public string HomeKey => FrameworkRuntimeStates.Fallback.Id;

    public Task<SiteModeHomeResult> BuildAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SiteModeHomeResult(
            "~/Views/SiteModes/Unassigned/Home.cshtml"));
    }
}
