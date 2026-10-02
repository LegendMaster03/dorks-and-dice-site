namespace dorks_and_dice_site.Models.Tools;

/// <summary>
/// Maps a Tool registration to its canonical public root. Proxied applications own a trailing-
/// slash root, while Embedded Modules use the host-rendered slug route directly.
/// </summary>
public static class ToolPublicRoute
{
    public static bool CanBuild(ToolRegistration tool) =>
        tool is not null
        && IsValidSlug(tool.Slug)
        && tool.IntegrationType is ToolIntegrationType.EmbeddedModule or ToolIntegrationType.ProxiedApplication;

    public static string GetPath(ToolRegistration tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (!IsValidSlug(tool.Slug))
        {
            throw new InvalidOperationException("A public Tool route requires a valid slug.");
        }

        return tool.IntegrationType switch
        {
            ToolIntegrationType.EmbeddedModule => $"/tools/{tool.Slug}",
            ToolIntegrationType.ProxiedApplication => $"/tools/{tool.Slug}/",
            _ => throw new InvalidOperationException("A public Tool route requires an application hosting type.")
        };
    }

    private static bool IsValidSlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return false;
        }

        var segmentStart = true;
        foreach (var character in slug)
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                segmentStart = false;
                continue;
            }

            if (character == '-' && !segmentStart)
            {
                segmentStart = true;
                continue;
            }

            return false;
        }

        return !segmentStart;
    }
}
