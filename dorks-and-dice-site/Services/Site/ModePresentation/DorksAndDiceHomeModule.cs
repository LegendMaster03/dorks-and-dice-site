using dorks_and_dice_site.Services.GameServers.Minecraft;

namespace dorks_and_dice_site.Services.Site.ModePresentation;

public sealed class DorksAndDiceHomeModule : ISiteModeHomeModule
{
    private readonly IConfiguration _configuration;
    private readonly IMinecraftServerStatusService _minecraftServerStatusService;

    public DorksAndDiceHomeModule(
        IConfiguration configuration,
        IMinecraftServerStatusService minecraftServerStatusService)
    {
        _configuration = configuration;
        _minecraftServerStatusService = minecraftServerStatusService;
    }

    public string HomeKey => BuiltInSiteModes.DorksAndDice.Id;

    public async Task<SiteModeHomeResult> BuildAsync(CancellationToken cancellationToken = default)
    {
        var viewData = new Dictionary<string, object?>
        {
            ["DiscordWidgetUrl"] = _configuration["Discord:WidgetUrl"],
            ["MinecraftServerStatus"] = await _minecraftServerStatusService.GetStatusAsync(cancellationToken)
        };

        return new SiteModeHomeResult(
            "~/Views/SiteModes/DorksAndDice/Home.cshtml",
            ViewData: viewData);
    }
}
