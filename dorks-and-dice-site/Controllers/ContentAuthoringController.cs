using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Authorize(Policy = AuthorizationPolicies.ModeEditor)]
[Route("editor/content")]
[RequestSizeLimit(ContentInputPolicy.MaxAuthoringRequestBytes)]
public sealed class ContentAuthoringController : ContentAuthoringControllerBase
{
    public ContentAuthoringController(
        IContentAuthoringService authoringService,
        IContentBodyRenderer bodyRenderer,
        IContentPageComposer pageComposer,
        IContentSourceRegistry sourceRegistry)
        : base(authoringService, bodyRenderer, pageComposer, sourceRegistry)
    {
    }

    protected override bool IsCentralAuthoring => false;
}
