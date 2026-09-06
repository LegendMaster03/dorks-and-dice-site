using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Authorize(Policy = AuthorizationPolicies.DevAccess)]
[Route("development/content/{slug}/media")]
[RequestSizeLimit(ContentInputPolicy.MaxAssetUploadBytes + 65_536)]
public sealed class DevelopmentContentMediaAuthoringController : ContentMediaAuthoringControllerBase
{
    public DevelopmentContentMediaAuthoringController(
        IContentAssetService assets,
        IContentSourceRegistry sourceRegistry,
        IContentAuthoringService authoringService)
        : base(assets, sourceRegistry, authoringService)
    {
    }

    protected override bool IsCentralAuthoring => true;
}
