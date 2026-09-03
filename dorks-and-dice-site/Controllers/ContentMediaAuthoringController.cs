@using dorks_and_dice_site.Models.Identity
@using dorks_and_dice_site.Models.Site
@using dorks_and_dice_site.Services.Identity
@using dorks_and_dice_site.Services.Site
@{
    ViewData["Title"] = "Editor";
    ViewData["Robots"] = "noindex,nofollow";
    var isGlobalEditor = User.IsInRole(AccountRoles.Admin);
    var mode = Context.GetSiteModeContext().SiteMode;
    var scopeLabel = isGlobalEditor
        ? "Head editor · all site scopes"
        : mode switch
        {
            SiteMode.DorksAndDice => "Dorks & Dice editor",
            SiteMode.Professional => "Professional editor",
            _ => "Scoped editor"
        };
}

<div class="mb-4">
    <p class="text-uppercase text-muted small mb-1">@scopeLabel</p>
    <h1 class="h2 mb-1">Editor</h1>
    <p class="text-body-secondary mb-0">Content authoring tools are separate from account administration and development infrastructure.</p>
</div>

<div class="row row-cols-1 row-cols-md-2 g-4">
    <div class="col">
        <a class="card h-100 text-decoration-none" href="/editor/content">
            <div class="card-body">
                <h2 class="h5">Content authoring</h2>
                <p class="text-body-secondary mb-0">Create, review, and revise pages within your editing scope.</p>
            </div>
        </a>
    </div>
    <div class="col">
        <a class="card h-100 text-decoration-none" href="/editor/media">
            <div class="card-body">
                <h2 class="h5">Media library</h2>
                <p class="text-body-secondary mb-0">Upload and reuse media for authored content.</p>
            </div>
        </a>
    </div>
</div>
