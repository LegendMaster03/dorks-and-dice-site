using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

/// <summary>
/// Development-only entry point for shared page authoring with explicit
/// cross-source selection; it does not confer central authority on ModeEditor.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.DevAccess)]
[Route("development/content")]
[RequestSizeLimit(ContentInputPolicy.MaxAuthoringRequestBytes)]
public sealed class DevelopmentContentAuthoringController : ContentAuthoringControllerBase
{
    public DevelopmentContentAuthoringController(
        IContentAuthoringService authoringService,
        IContentBodyRenderer bodyRenderer,
        IContentPageComposer pageComposer,
        IContentSourceRegistry sourceRegistry)
        : base(authoringService, bodyRenderer, pageComposer, sourceRegistry)
    {
    }

    protected override bool IsCentralAuthoring => true;
}
