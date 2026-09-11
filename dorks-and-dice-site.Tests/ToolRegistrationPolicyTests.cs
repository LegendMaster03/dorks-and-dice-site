using dorks_and_dice_site.Models.Tools;

namespace dorks_and_dice_site.Tests;

public sealed class ToolRegistrationPolicyTests
{
    [Fact]
    public void EmbeddedRulesCoreRegistrationIsAlwaysAnonymousReadable()
    {
        var registration = new ToolRegistration
        {
            Slug = "rules-core",
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            AllowAnonymous = false
        };

        Assert.True(registration.AllowAnonymous);

        registration.Slug = "RULES-CORE";
        registration.AllowAnonymous = false;
        Assert.True(registration.AllowAnonymous);
    }

    [Fact]
    public void ProxiedRulesCoreRegistrationStillHonorsConfiguredVisibility()
    {
        var registration = new ToolRegistration
        {
            Slug = "rules-core",
            IntegrationType = ToolIntegrationType.ProxiedApplication,
            AllowAnonymous = false
        };

        Assert.False(registration.AllowAnonymous);
    }

    [Fact]
    public void OtherToolsStillRespectConfiguredAnonymousVisibility()
    {
        var registration = new ToolRegistration
        {
            Slug = "private-tool",
            AllowAnonymous = false
        };

        Assert.False(registration.AllowAnonymous);

        registration.AllowAnonymous = true;
        Assert.True(registration.AllowAnonymous);
    }
}
