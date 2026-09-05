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
            foreach (var key in parameters.Keys)
            {
                if (!string.Equals(key, "title", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"The discord-widget page component does not support parameter '{key}'.");
                }
            }

            if (parameters.TryGetValue("title", out var title)
                && title.Length > ContentInputPolicy.MaxTitleLength)
            {
                throw new InvalidOperationException(
                    $"The discord-widget title exceeds {ContentInputPolicy.MaxTitleLength:N0} characters.");
            }
        }
    }
}

public sealed record DiscordWidgetViewModel(string? WidgetUrl, string Title);

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
        var title = request.GetOptionalParameter("title");
        if (string.IsNullOrWhiteSpace(title))
        {
            title = "Discord Server";
        }

        return View(
            "~/Views/Plugins/DiscordWidget/Default.cshtml",
            new DiscordWidgetViewModel(widgetUrl, title));
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
