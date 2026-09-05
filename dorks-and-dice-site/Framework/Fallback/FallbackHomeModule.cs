using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Framework.Fallback;

public sealed class FallbackHomeModule : ISiteModeHomeModule
{
    public string HomeKey => FrameworkRuntimeStates.Fallback.Id;

    public Task<SiteModeHomeResult> BuildAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SiteModeHomeResult(
            "~/Views/SiteModes/Unassigned/Home.cshtml"));
    }
}
