using dorks_and_dice_site.Controllers;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Tests;

public sealed class ResumeControllerTests
{
    [Fact]
    public async Task IndexUsesSharedHomepageResolution()
    {
        var expectedModel = new object();
        var service = new StubHomeService(new SiteModeHomeResult(
            "~/Views/Content/Homepage.cshtml",
            expectedModel,
            new Dictionary<string, object?>
            {
                ["TestValue"] = "shared-home"
            }));
        var controller = new ResumeController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.HttpContext.Items[SiteModeContext.HttpContextItemKey] = new SiteModeContext
        {
            ActiveMode = BuiltInSiteModes.Professional
        };

        var result = Assert.IsType<ViewResult>(await controller.Index(CancellationToken.None));

        Assert.Equal("~/Views/Content/Homepage.cshtml", result.ViewName);
        Assert.Same(expectedModel, result.Model);
        Assert.Equal("shared-home", controller.ViewData["TestValue"]);
        Assert.Same(controller.HttpContext.GetSiteModeContext(), service.ReceivedContext);
    }

    private sealed class StubHomeService : ISiteModeHomeService
    {
        private readonly SiteModeHomeResult _result;

        public StubHomeService(SiteModeHomeResult result)
        {
            _result = result;
        }

        public SiteModeContext? ReceivedContext { get; private set; }

        public Task<SiteModeHomeResult> GetHomeAsync(
            SiteModeContext context,
            CancellationToken cancellationToken = default)
        {
            ReceivedContext = context;
            return Task.FromResult(_result);
        }
    }
}
