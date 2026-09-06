using dorks_and_dice_site.Framework.Plugins;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Plugins.DiscordWidget;

public sealed class DiscordWidgetPlugin : ISitePlugin
{
    public SitePluginManifest Manifest { get; } = new(
        Id: "discord-widget",
        DisplayName: "Discord Widget",
        Version: "1.1.0");

    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IContentPageComponentDefinition, DiscordWidgetPageComponentDefinition>();
    }

    private sealed class DiscordWidgetPageComponentDefinition : IContentPageComponentDefinition
    {
        public string Name => "discord-widget";
        public string ViewComponentName => "DiscordWidget";

        public void Validate(IReadOnlyDictionary<string, string> parameters)
        {
            foreach (var key in parameters.Keys)
            {
                if (!string.Equals(key, "server-id", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(key, "theme", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(key, "title", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"The discord-widget page component does not support parameter '{key}'.");
                }
            }

            if (!parameters.TryGetValue("server-id", out var serverId)
                || !IsValidServerId(serverId))
            {
                throw new InvalidOperationException(
                    "The discord-widget page component requires a valid numeric server-id.");
            }

            if (parameters.TryGetValue("theme", out var theme)
                && !string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The discord-widget theme must be either 'dark' or 'light'.");
            }

            if (parameters.TryGetValue("title", out var title)
                && title.Length > ContentInputPolicy.MaxTitleLength)
            {
                throw new InvalidOperationException(
                    $"The discord-widget title exceeds {ContentInputPolicy.MaxTitleLength:N0} characters.");
            }
        }

        private static bool IsValidServerId(string serverId) =>
            ulong.TryParse(serverId, out var parsed) && parsed > 0;
    }
}

public sealed record DiscordWidgetViewModel(string WidgetUrl, string Title);

public sealed class DiscordWidgetViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(ContentPageComponentInvocation request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var serverId = request.GetOptionalParameter("server-id")
            ?? throw new InvalidOperationException(
                "The discord-widget page component requires a server-id.");
        if (!ulong.TryParse(serverId, out var parsedServerId) || parsedServerId == 0)
        {
            throw new InvalidOperationException(
                "The discord-widget page component requires a valid numeric server-id.");
        }

        var theme = request.GetOptionalParameter("theme");
        theme = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase)
            ? "light"
            : "dark";

        var title = request.GetOptionalParameter("title");
        if (string.IsNullOrWhiteSpace(title))
        {
            title = "Discord Server";
        }

        var widgetUrl = $"https://discord.com/widget?id={parsedServerId}&theme={theme}";
        return View(
            "~/Views/Plugins/DiscordWidget/Default.cshtml",
            new DiscordWidgetViewModel(widgetUrl, title));
    }
}
