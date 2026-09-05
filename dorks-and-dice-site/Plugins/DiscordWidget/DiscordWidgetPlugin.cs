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
        Version: "1.0.0");

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
            if (parameters.Count != 0)
            {
                throw new InvalidOperationException(
                    "The discord-widget page component does not accept authored parameters. Its URL is deployment configuration.");
            }
        }
    }
}

public sealed class DiscordWidgetViewComponent : ViewComponent
{
    private readonly IConfiguration _configuration;

    public DiscordWidgetViewComponent(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IViewComponentResult Invoke(ContentPageComponentInvocation request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var configuredUrl = _configuration["Discord:WidgetUrl"];
        var widgetUrl = TryGetSafeWidgetUrl(configuredUrl);
        return View("~/Views/Plugins/DiscordWidget/Default.cshtml", widgetUrl);
    }

    private static string? TryGetSafeWidgetUrl(string? configuredUrl)
    {
        if (string.IsNullOrWhiteSpace(configuredUrl)
            || !Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return uri.AbsoluteUri;
    }
}
