namespace dorks_and_dice_site.Services.Site;

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
