using dorks_and_dice_site.Models.Tools;

namespace dorks_and_dice_site.Tests;

public sealed class ToolPublicRouteTests
{
    [Theory]
    [InlineData(ToolIntegrationType.EmbeddedModule, "/tools/block-initiative")]
    [InlineData(ToolIntegrationType.ProxiedApplication, "/tools/block-initiative/")]
    public void GetPathUsesIntegrationSpecificCanonicalRoot(
        ToolIntegrationType integrationType,
        string expected)
    {
        var tool = new ToolRegistration
        {
            Slug = "block-initiative",
            IntegrationType = integrationType
        };

        Assert.True(ToolPublicRoute.CanBuild(tool));
        Assert.Equal(expected, ToolPublicRoute.GetPath(tool));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Block-Initiative")]
    [InlineData("block--initiative")]
    [InlineData("block-initiative-")]
    [InlineData("block/initiative")]
    [InlineData("../block-initiative")]
    public void InvalidSlugCanNotProducePublicRoute(string slug)
    {
        var tool = new ToolRegistration
        {
            Slug = slug,
            IntegrationType = ToolIntegrationType.EmbeddedModule
        };

        Assert.False(ToolPublicRoute.CanBuild(tool));
        Assert.Throws<InvalidOperationException>(() => ToolPublicRoute.GetPath(tool));
    }
}
