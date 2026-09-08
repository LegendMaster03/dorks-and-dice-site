using dorks_and_dice_site.Models.Campaigns;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Tools;

namespace dorks_and_dice_site.Tests;

public sealed class ToolAuthenticationTicketTests
{
    [Fact]
    public void TicketIsToolScopedAndSingleUse()
    {
        var context = Context("rules-core");
        var ticket = ToolAuthenticationTickets.Issue(context);

        Assert.False(ToolAuthenticationTickets.TryRedeem("another-tool", ticket, out _));
        Assert.False(ToolAuthenticationTickets.TryRedeem("rules-core", ticket, out _));

        ticket = ToolAuthenticationTickets.Issue(context);
        Assert.True(ToolAuthenticationTickets.TryRedeem("rules-core", ticket, out var redeemed));
        Assert.Same(context, redeemed);
        Assert.False(ToolAuthenticationTickets.TryRedeem("rules-core", ticket, out _));
    }

    [Fact]
    public void TicketCarriesSeparateGlobalAndCampaignAuthorizationContext()
    {
        var context = Context("rules-core");
        var ticket = ToolAuthenticationTickets.Issue(context);

        Assert.True(ToolAuthenticationTickets.TryRedeem("rules-core", ticket, out var redeemed));
        Assert.NotNull(redeemed);
        Assert.Contains(AccountRoles.RulesLawyer, redeemed.GlobalRoles);
        var campaign = Assert.Single(redeemed.Campaigns);
        Assert.Equal(CampaignRoles.Dm, campaign.Role);
        Assert.Equal("user-123", redeemed.User.Id);
    }

    private static ToolHostAuthenticationContext Context(string slug) => new()
    {
        ToolSlug = slug,
        SiteMode = "dorks-and-dice",
        User = new ToolHostUserContext
        {
            Id = "user-123",
            DisplayName = "Rules Lawyer"
        },
        GlobalRoles = [AccountRoles.RulesLawyer],
        Campaigns =
        [
            new CampaignAccessSummary
            {
                Id = Guid.NewGuid(),
                Name = "Test Campaign",
                Role = CampaignRoles.Dm
            }
        ]
    };
}
