using dorks_and_dice_site.Models.Campaigns;
using dorks_and_dice_site.Services.Campaigns;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace dorks_and_dice_site.Tests;

public sealed class CampaignAccessStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), $"dorks-and-dice-campaign-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task UserOnlyReceivesCampaignsWithExplicitMembership()
    {
        var store = CreateStore();
        var included = Campaign("Included Campaign");
        var excluded = Campaign("Excluded Campaign");
        await store.SaveCampaignAsync(included);
        await store.SaveCampaignAsync(excluded);
        await store.SaveMembershipAsync(new CampaignMembershipRecord
        {
            CampaignId = included.Id,
            UserId = "user-a",
            Role = CampaignRoles.Player
        });
        await store.SaveMembershipAsync(new CampaignMembershipRecord
        {
            CampaignId = excluded.Id,
            UserId = "user-b",
            Role = CampaignRoles.Dm
        });

        var campaigns = await store.GetCampaignsForUserAsync("user-a");

        var campaign = Assert.Single(campaigns);
        Assert.Equal(included.Id, campaign.Id);
        Assert.Equal("Included Campaign", campaign.Name);
        Assert.Equal(CampaignRoles.Player, campaign.Role);
    }

    [Fact]
    public async Task DisabledCampaignIsNotExposedToMember()
    {
        var store = CreateStore();
        var campaign = Campaign("Disabled Campaign");
        campaign.Enabled = false;
        await store.SaveCampaignAsync(campaign);
        await store.SaveMembershipAsync(new CampaignMembershipRecord
        {
            CampaignId = campaign.Id,
            UserId = "user-a",
            Role = CampaignRoles.Dm
        });

        var campaigns = await store.GetCampaignsForUserAsync("user-a");

        Assert.Empty(campaigns);
    }

    [Fact]
    public async Task MembershipRoleMustBeSupportedCampaignRole()
    {
        var store = CreateStore();
        var campaign = Campaign("Role Test");
        await store.SaveCampaignAsync(campaign);

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveMembershipAsync(
            new CampaignMembershipRecord
            {
                CampaignId = campaign.Id,
                UserId = "user-a",
                Role = "Admin"
            }));
    }

    [Fact]
    public async Task DeletingCampaignAlsoRemovesMembershipAccess()
    {
        var store = CreateStore();
        var campaign = Campaign("Delete Test");
        await store.SaveCampaignAsync(campaign);
        await store.SaveMembershipAsync(new CampaignMembershipRecord
        {
            CampaignId = campaign.Id,
            UserId = "user-a",
            Role = CampaignRoles.Dm
        });

        Assert.True(await store.DeleteCampaignAsync(campaign.Id));
        Assert.Empty(await store.GetCampaignsForUserAsync("user-a"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private JsonCampaignAccessStore CreateStore()
    {
        Directory.CreateDirectory(_directory);
        var environment = new TestHostEnvironment
        {
            ContentRootPath = _directory,
            ContentRootFileProvider = new PhysicalFileProvider(_directory)
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [JsonCampaignAccessStore.StoragePathConfigurationKey] = "campaign-access.json"
            })
            .Build();
        return new JsonCampaignAccessStore(environment, configuration);
    }

    private static CampaignRecord Campaign(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Enabled = true
    };

    private sealed class TestHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "dorks-and-dice-site.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
