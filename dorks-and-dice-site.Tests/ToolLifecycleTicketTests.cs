using dorks_and_dice_site.Services.Tools;

namespace dorks_and_dice_site.Tests;

public sealed class ToolLifecycleTicketTests
{
    [Fact]
    public void LifecycleTicketIsToolScopedAndSingleUse()
    {
        var context = Context("character-sheet");
        var ticket = ToolLifecycleTickets.Issue(context);

        Assert.False(ToolLifecycleTickets.TryRedeem("rules-core", ticket, out _));
        Assert.False(ToolLifecycleTickets.TryRedeem("character-sheet", ticket, out _));

        ticket = ToolLifecycleTickets.Issue(context);
        Assert.True(ToolLifecycleTickets.TryRedeem("character-sheet", ticket, out var redeemed));
        Assert.Equal(context, redeemed);
        Assert.False(ToolLifecycleTickets.TryRedeem("character-sheet", ticket, out _));
    }

    [Fact]
    public void LifecycleTicketCarriesOnlyLifecycleContext()
    {
        var context = Context("character-sheet");
        var ticket = ToolLifecycleTickets.Issue(context);

        Assert.True(ToolLifecycleTickets.TryRedeem("character-sheet", ticket, out var redeemed));
        Assert.NotNull(redeemed);
        Assert.Equal(1, redeemed.ContractVersion);
        Assert.Equal(context.EventId, redeemed.EventId);
        Assert.Equal("character.deleted", redeemed.EventType);
        Assert.Equal(context.SubjectId, redeemed.SubjectId);
    }

    private static ToolLifecycleContext Context(string slug) => new(
        1,
        slug,
        Guid.NewGuid(),
        "character.deleted",
        Guid.NewGuid(),
        DateTimeOffset.UtcNow);
}
