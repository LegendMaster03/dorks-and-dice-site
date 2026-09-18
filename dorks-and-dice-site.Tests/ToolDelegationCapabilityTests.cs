using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;

namespace dorks_and_dice_site.Tests;

public sealed class ToolDelegationCapabilityTests
{
    [Fact]
    public void CapabilityIsSourceBoundReusableAndDistinctFromNormalToolTicket()
    {
        var service = new ToolDelegationCapabilityService();
        var context = Context("character-sheet");
        var capability = service.Issue("character-sheet", context);

        Assert.StartsWith("ddtd_v1_", capability, StringComparison.Ordinal);
        Assert.True(service.TryUse("character-sheet", capability, out var first));
        Assert.Same(context, first);
        Assert.True(service.TryUse("character-sheet", capability, out var second));
        Assert.Same(context, second);
        Assert.False(service.TryUse("rules-core", capability, out _));
        Assert.False(ToolAuthenticationTickets.TryRedeem(
            "character-sheet",
            capability,
            out _));

        var normalTicket = ToolAuthenticationTickets.Issue(context);
        Assert.False(service.TryUse("character-sheet", normalTicket, out _));
        Assert.True(ToolAuthenticationTickets.TryRedeem(
            "character-sheet",
            normalTicket,
            out var redeemed));
        Assert.Same(context, redeemed);
    }

    [Fact]
    public void InvalidAndExpiredCapabilitiesAreRejected()
    {
        var clock = new ManualTimeProvider(
            new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero));
        var service = new ToolDelegationCapabilityService(clock);
        var capability = service.Issue("character-sheet", Context("character-sheet"));

        Assert.False(service.TryUse("character-sheet", "not-a-capability", out _));

        clock.Advance(ToolDelegationCapabilityService.Lifetime);
        Assert.False(service.TryUse("character-sheet", capability, out _));
    }

    [Fact]
    public void CapabilityHasBoundedUseCount()
    {
        var service = new ToolDelegationCapabilityService();
        var capability = service.Issue("character-sheet", Context("character-sheet"));

        for (var i = 0; i < ToolDelegationCapabilityService.MaximumUses; i++)
        {
            Assert.True(service.TryUse("character-sheet", capability, out _));
        }

        Assert.False(service.TryUse("character-sheet", capability, out _));
    }

    [Fact]
    public void CapabilityCanNotBeIssuedForMismatchedSourceContext()
    {
        var service = new ToolDelegationCapabilityService();

        Assert.Throws<ArgumentException>(() =>
            service.Issue("rules-core", Context("character-sheet")));
    }

    private static ToolHostAuthenticationContext Context(string slug) => new()
    {
        ToolSlug = slug,
        SiteMode = SiteModeValues.DorksAndDiceModeValue,
        User = new ToolHostUserContext
        {
            Id = Guid.NewGuid().ToString("D"),
            DisplayName = "Delegation Test User"
        }
    };

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }
}
