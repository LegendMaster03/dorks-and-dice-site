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
        Version: "1.1.0");

    public void RegisterServices(IServiceCollection services)
    {
        services.AddOptions<MinecraftServerOptions>()
            .BindConfiguration(MinecraftServerOptions.SectionName);
        services.AddSingleton<IMinecraftServerStatusService, MinecraftServerStatusService>();
        services.AddSingleton<MinecraftServerStatusSnapshotStore>();
        services.AddSingleton<IMinecraftServerStatusSnapshotStore>(serviceProvider =>
            serviceProvider.GetRequiredService<MinecraftServerStatusSnapshotStore>());
        services.AddHostedService<MinecraftServerStatusPoller>();

        services.AddSingleton<IContentPageComponentDefinition>(
            new MinecraftServerStatusPageComponentDefinition(
                "minecraft-server-status",
                "MinecraftServerStatus"));
        services.AddSingleton<IContentPageComponentDefinition>(
            new MinecraftServerStatusPageComponentDefinition(
                "minecraft-server-status-badge",
                "MinecraftServerStatusField"));
        services.AddSingleton<IContentPageComponentDefinition>(
            new MinecraftServerStatusPageComponentDefinition(
                "minecraft-server-motd",
                "MinecraftServerStatusField"));
        services.AddSingleton<IContentPageComponentDefinition>(
            new MinecraftServerStatusPageComponentDefinition(
                "minecraft-server-online-players",
                "MinecraftServerStatusField"));
        services.AddSingleton<IContentPageComponentDefinition>(
            new MinecraftServerStatusPageComponentDefinition(
                "minecraft-server-maximum-players",
                "MinecraftServerStatusField"));
        services.AddSingleton<IContentPageComponentDefinition>(
            new MinecraftServerStatusPageComponentDefinition(
                "minecraft-server-players",
                "MinecraftServerStatusField"));
        services.AddSingleton<IContentPageComponentDefinition>(
            new MinecraftServerStatusPageComponentDefinition(
                "minecraft-server-version",
                "MinecraftServerStatusField"));
    }

    private sealed class MinecraftServerStatusPageComponentDefinition(
        string name,
        string viewComponentName) : IContentPageComponentDefinition
    {
        public string Name { get; } = name;
        public string ViewComponentName { get; } = viewComponentName;

        public void Validate(IReadOnlyDictionary<string, string> parameters)
        {
            if (parameters.Count == 0)
            {
                return;
            }

            var unsupported = parameters.Keys.Order(StringComparer.OrdinalIgnoreCase).First();
            throw new InvalidOperationException(
                $"Content page component '{Name}' does not support parameter '{unsupported}'.");
        }
    }
}

public sealed class MinecraftServerStatusViewComponent : ViewComponent
{
    private readonly IMinecraftServerStatusSnapshotStore _store;

    public MinecraftServerStatusViewComponent(IMinecraftServerStatusSnapshotStore store)
    {
        _store = store;
    }

    public IViewComponentResult Invoke(ContentPageComponentInvocation request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return View("~/Views/Plugins/MinecraftServerStatus/Default.cshtml", _store.Current);
    }
}

public sealed class MinecraftServerStatusFieldViewComponent : ViewComponent
{
    private readonly IMinecraftServerStatusSnapshotStore _store;

    public MinecraftServerStatusFieldViewComponent(IMinecraftServerStatusSnapshotStore store)
    {
        _store = store;
    }

    public IViewComponentResult Invoke(ContentPageComponentInvocation request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var field = request.Name.ToLowerInvariant() switch
        {
            "minecraft-server-status-badge" => "badge",
            "minecraft-server-motd" => "motd",
            "minecraft-server-online-players" => "online-players",
            "minecraft-server-maximum-players" => "maximum-players",
            "minecraft-server-players" => "players",
            "minecraft-server-version" => "version",
            _ => throw new InvalidOperationException(
                $"Unsupported Minecraft status field component '{request.Name}'.")
        };

        return View(
            "~/Views/Plugins/MinecraftServerStatus/Field.cshtml",
            new MinecraftServerStatusFieldViewModel(field, _store.Current));
    }
}
