using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ContentMoveSafetyTests
{
    [Fact]
    public async Task MoveRefusesToReplaceExistingTargetHistory()
    {
        using var fixture = new MoveFixture();
        var authoring = new ContentAuthoringService(fixture.Registry);

        var source = authoring.GetNew("Source");
        source.Document.Id = "stable-page";
        source.Document.Slug = "stable-page";
        await authoring.CreateAsync(source.Document);

        var target = authoring.GetNew("Target");
        target.Document.Id = "stable-page";
        target.Document.Slug = "stable-page";
        await authoring.CreateAsync(target.Document);

        var targetEdit = await authoring.GetEditAsync("Target", "stable-page");
        Assert.NotNull(targetEdit);
        targetEdit.Document.Body = "Target history must survive.";
        await authoring.SaveRevisionAsync(targetEdit.Document);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => authoring.MoveAsync("Source", "Target", "stable-page"));

        Assert.Contains("do not replace existing target history", error.Message);
        Assert.NotNull(await authoring.GetEditAsync("Source", "stable-page"));

        var unchangedTarget = await authoring.GetEditAsync("Target", "stable-page");
        Assert.NotNull(unchangedTarget);
        Assert.Contains("Target history must survive.", unchangedTarget.Document.Body);
        Assert.Equal(2, unchangedTarget.History.Count);
    }

    private sealed class MoveFixture : IDisposable
    {
        private readonly string _directory;

        public MoveFixture()
        {
            _directory = Path.Combine(Path.GetTempPath(), $"content-move-safety-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:SourceDb"] = "Data Source=source.db",
                ["ConnectionStrings:TargetDb"] = "Data Source=target.db",
                ["ContentStorage:AuthoringSource"] = "Source",
                ["ContentStorage:Sources:Source:Provider"] = "Sqlite",
                ["ContentStorage:Sources:Source:ConnectionString"] = "SourceDb",
                ["ContentStorage:Sources:Target:Provider"] = "Sqlite",
                ["ContentStorage:Sources:Target:ConnectionString"] = "TargetDb",
                ["ContentStorage:GlobalSources:0"] = "Target"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
            Registry = new ContentSourceRegistry(configuration, _directory);
        }

        public ContentSourceRegistry Registry { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, recursive: true);
                }
            }
            catch (IOException)
            {
                // SQLite can briefly hold a file handle on Windows after disposal.
            }
        }
    }
}
