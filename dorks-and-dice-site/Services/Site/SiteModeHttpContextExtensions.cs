namespace dorks_and_dice_site.Services.Site;

/// <summary>
/// Retrieves middleware-resolved mode context. Before resolution, the safe
/// default is framework fallback rather than either live hosted site.
/// </summary>
public static class SiteModeHttpContextExtensions
{
    public static SiteModeContext GetSiteModeContext(this HttpContext context)
    {
        return context.Items[SiteModeContext.HttpContextItemKey] as SiteModeContext
            ?? new SiteModeContext
            {
                FrameworkState = FrameworkRuntimeStates.Fallback
            };
    }
}
