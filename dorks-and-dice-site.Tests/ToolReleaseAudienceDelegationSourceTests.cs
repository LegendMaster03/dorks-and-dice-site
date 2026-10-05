using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class ToolReleaseAudienceDelegationSourceTests(
    PublishedContentWebApplicationFactory factory)
{
    [Fact]
    public async Task DelegatedAndPrivateTunnelContextsReevaluateCurrentSourceAudience()
    {
        var source = Application(ToolReleaseAudience.Public);
        var target = Application(ToolReleaseAudience.Public);
        await SaveAsync(source);

        try
        {
            using var scope = factory.Services.CreateScope();
            var contextFactory = scope.ServiceProvider
                .GetRequiredService<IToolHostAuthenticationContextFactory>();

            var testerContext = HostContext(
                source,
                globalRoles: [],
                scopedRoles: [ScopedAccountRoles.Tester]);

            Assert.NotNull(await contextFactory.CreateDelegatedAsync(target, testerContext));
            Assert.NotNull(await contextFactory.CreatePrivateTunnelAsync(target, testerContext));

            source.ReleaseAudience = ToolReleaseAudience.Development;
            source.AllowAnonymous = false;
            source.UpdatedAt = DateTimeOffset.UtcNow;
            await SaveAsync(source);

            Assert.Null(await contextFactory.CreateDelegatedAsync(target, testerContext));
            Assert.Null(await contextFactory.CreatePrivateTunnelAsync(target, testerContext));

            var devContext = HostContext(
                source,
                globalRoles: [AccountRoles.Dev],
                scopedRoles: [ScopedAccountRoles.Tester]);

            Assert.NotNull(await contextFactory.CreateDelegatedAsync(target, devContext));
            Assert.NotNull(await contextFactory.CreatePrivateTunnelAsync(target, devContext));
        }
        finally
        {
            await DeleteAsync(source.Id);
        }
    }

    private static ToolRegistration Application(ToolReleaseAudience audience)
    {
        var slug = $"release-source-{Guid.NewGuid():N}";
        return new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Key = slug,
            Kind = ToolKind.Application,
            Slug = slug,
            DisplayName = "Release source test",
            Modes = [BuiltInSiteModes.DorksAndDice.Id],
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            IntegrationContractVersion = ToolIntegrationContractVersions.EmbeddedModuleCurrent,
            UpstreamBaseUrl = "http://release-source-test:8080",
            FrontendEntryPoint = "/app.js",
            ReleaseAudience = audience,
            AllowAnonymous = audience == ToolReleaseAudience.Public,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static ToolHostAuthenticationContext HostContext(
        ToolRegistration source,
        IReadOnlyList<string> globalRoles,
        IReadOnlyList<string> scopedRoles) => new()
    {
        ToolKey = source.Key,
        ToolSlug = source.Slug,
        SiteMode = BuiltInSiteModes.DorksAndDice.Id,
        User = new ToolHostUserContext
        {
            Id = TestRoleAuthenticationHandler.DefaultUserId,
            DisplayName = "Release source user"
        },
        GlobalRoles = globalRoles,
        ScopedRoles = scopedRoles
    };

    private async Task SaveAsync(ToolRegistration tool)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IToolRegistry>().SaveAsync(tool);
    }

    private async Task DeleteAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IToolRegistry>().DeleteAsync(id);
    }
}
