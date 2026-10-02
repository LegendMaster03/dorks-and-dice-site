namespace dorks_and_dice_site.Models.Tools;

/// <summary>
/// Maps a Tool registration to its canonical public root. Proxied applications own a trailing-
/// slash root, while Embedded Modules use the host-rendered slug route directly.
/// </summary>
public static class ToolPublicRoute
{
    public static string GetPath(ToolRegistration tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (string.IsNullOrWhiteSpace(tool.Slug))
        {
            throw new InvalidOperationException("A public Tool route requires a slug.");
        }

        return tool.IntegrationType switch
        {
            ToolIntegrationType.EmbeddedModule => $"/tools/{tool.Slug}",
            ToolIntegrationType.ProxiedApplication => $"/tools/{tool.Slug}/",
            _ => throw new InvalidOperationException("A public Tool route requires an application hosting type.")
        };
    }
}
