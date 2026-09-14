using dorks_and_dice_site.Modes.DorksAndDice;
using dorks_and_dice_site.Modes.Professional;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Routing;

namespace dorks_and_dice_site.Tests;

public sealed class SiteModeViewLocationExpanderTests
{
    [Fact]
    public void ActiveModePrependsModeSpecificControllerAndSharedLocations()
    {
        var expander = new SiteModeViewLocationExpander();
        var context = CreateContext(DorksAndDiceMode.Definition);

        expander.PopulateValues(context);
        var locations = expander.ExpandViewLocations(
                context,
                ["/Views/{1}/{0}.cshtml", "/Views/Shared/{0}.cshtml"])
            .ToArray();

        Assert.Equal("/Views/SiteModes/DorksAndDice/{1}/{0}.cshtml", locations[0]);
        Assert.Equal("/Views/SiteModes/DorksAndDice/Shared/{0}.cshtml", locations[1]);
        Assert.Equal("/Views/{1}/{0}.cshtml", locations[2]);
        Assert.Equal("/Views/Shared/{0}.cshtml", locations[3]);
    }

    [Fact]
    public void ViewCacheKeyVariesByActiveMode()
    {
        var expander = new SiteModeViewLocationExpander();
        var dorksContext = CreateContext(DorksAndDiceMode.Definition);
        var professionalContext = CreateContext(ProfessionalMode.Definition);

        expander.PopulateValues(dorksContext);
        expander.PopulateValues(professionalContext);

        Assert.NotEqual(
            dorksContext.Values.Single().Value,
            professionalContext.Values.Single().Value);
    }

    [Fact]
    public void FrameworkFallbackKeepsNormalMvcLocationsOnly()
    {
        var expander = new SiteModeViewLocationExpander();
        var context = CreateContext(activeMode: null);
        string[] defaults = ["/Views/{1}/{0}.cshtml", "/Views/Shared/{0}.cshtml"];

        expander.PopulateValues(context);
        var locations = expander.ExpandViewLocations(context, defaults).ToArray();

        Assert.Equal(defaults, locations);
    }

    private static ViewLocationExpanderContext CreateContext(SiteModeDefinition? activeMode)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[SiteModeContext.HttpContextItemKey] = new SiteModeContext
        {
            ActiveMode = activeMode,
            FrameworkState = activeMode is null ? FrameworkRuntimeStates.Fallback : null
        };

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());

        return new ViewLocationExpanderContext(
            actionContext,
            viewName: "Index",
            controllerName: "Campaigns",
            areaName: null,
            pageName: null,
            isMainPage: true)
        {
            Values = new Dictionary<string, string?>()
        };
    }
}
