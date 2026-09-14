using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Modes.DorksAndDice.Persistence;

public static class DorksAndDiceStorageOptions
{
    public const string SectionName = "DorksAndDiceStorage";
}

public sealed class DorksAndDiceStorageInitializer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration) : IHostedService
{
    private static readonly SemaphoreSlim InitializationGate = new(1, 1);

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IConfiguration _configuration = configuration;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_configuration.GetValue($"{DorksAndDiceStorageOptions.SectionName}:EnsureCreatedOnStartup", true))
        {
            return;
        }

        await InitializationGate.WaitAsync(cancellationToken);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DorksAndDiceDbContext>();
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }
        finally
        {
            InitializationGate.Release();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
