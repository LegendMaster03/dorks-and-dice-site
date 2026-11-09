using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

/// <summary>
/// Mode-scoped editor route for the shared content authoring implementation.
/// Unlike Development authoring, this entry point is bounded by ModeEditor.
/// </summary>
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
