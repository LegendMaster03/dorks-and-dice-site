using dorks_and_dice_site.Framework.Plugins;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Plugins.MinecraftServerStatus;

public sealed class MinecraftServerStatusPlugin : ISitePlugin
{
    public SitePluginManifest Manifest { get; } = new(
        Id: "minecraft-server-status",
        DisplayName: "Minecraft Server Status",
        Version: "1.0.0");

    public void RegisterServices(IServiceCollection services)
    {
        services.AddOptions<MinecraftServerOptions>()
            .BindConfiguration(MinecraftServerOptions.SectionName);
        services.AddSingleton<IMinecraftServerStatusService, MinecraftServerStatusService>();
        services.AddSingleton<IContentPageComponentDefinition, MinecraftServerStatusPageComponentDefinition>();
    }

    private sealed class MinecraftServerStatusPageComponentDefinition : IContentPageComponentDefinition
    {
        public string Name => "minecraft-server-status";
        public string ViewComponentName => "MinecraftServerStatus";

        public void Validate(IReadOnlyDictionary<string, string> parameters)
        {
            if (parameters.Count == 0)
            {
                return;
            }

            var unsupported = parameters.Keys.Order(StringComparer.OrdinalIgnoreCase).First();
            throw new InvalidOperationException(
                $"The minecraft-server-status page component does not support parameter '{unsupported}'.");
        }
    }
}

public sealed class MinecraftServerStatusViewComponent : ViewComponent
{
    private readonly IMinecraftServerStatusService _statusService;

    public MinecraftServerStatusViewComponent(IMinecraftServerStatusService statusService)
    {
        _statusService = statusService;
    }

    public async Task<IViewComponentResult> InvokeAsync(ContentPageComponentInvocation request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var status = await _statusService.GetStatusAsync(HttpContext.RequestAborted);
        return View("~/Views/Plugins/MinecraftServerStatus/Default.cshtml", status);
    }
}
