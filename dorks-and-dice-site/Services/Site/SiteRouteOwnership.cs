@model dorks_and_dice_site.Models.Content.ContentAuthoringIndexViewModel
@{
    ViewData["Title"] = "Database Management";
    ViewData["Robots"] = "noindex,nofollow";
    var selectedSource = Model.Sources.FirstOrDefault(source =>
        string.Equals(source.Key, Model.SelectedSourceKey, StringComparison.OrdinalIgnoreCase));
}

<div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-4">
    <div>
        <p class="text-uppercase text-muted small mb-1">Development</p>
        <h1 class="h3 mb-0">Database management</h1>
    </div>
    <a class="btn btn-outline-secondary" href="/development">Back to Development</a>
</div>

<p class="text-muted">Inspect configured content databases and perform explicit per-page transfers. Content creation and editing are handled separately by the Editor role.</p>

@if (TempData["ContentDatabaseSuccess"] is string successMessage)
{
    <div class="alert alert-success" role="status">@successMessage</div>
}
@if (TempData["ContentDatabaseError"] is string errorMessage)
{
    <div class="alert alert-warning" role="alert">@errorMessage</div>
}

@if (Model.Sources.Count > 1)
{
    <form method="get" action="/development/databases" class="row g-3 align-items-end mb-4">
        <div class="col-sm-8 col-md-5">
            <label class="form-label" for="source">Content database</label>
            <select class="form-select" id="source" name="source" data-auto-submit="change">
                @foreach (var source in Model.Sources)
                {
                    <option value="@source.Key" selected="@(string.Equals(source.Key, Model.SelectedSourceKey, StringComparison.OrdinalIgnoreCase))">
                        @source.DisplayName
                    </option>
                }
            </select>
        </div>
    </form>
}
else
{
    <div class="card card-body mb-4">
        <div class="small text-uppercase text-muted">Configured content database</div>
        <div class="fw-semibold">@(selectedSource?.DisplayName ?? Model.SelectedSourceKey)</div>
        <div class="small text-muted">No database selector is shown because only one source is configured.</div>
    </div>
}

@if (Model.Items.Count == 0)
{
    <p class="text-muted">This database contains no content pages.</p>
}
else
{
    <div class="table-responsive">
        <table class="table align-middle">
            <thead>
                <tr>
                    <th scope="col">Title</th>
                    <th scope="col">Slug</th>
                    <th scope="col">Revision</th>
                    <th scope="col">Listing</th>
                    @if (Model.MoveTargets.Count > 0)
                    {
                        <th scope="col">Transfer</th>
                    }
                </tr>
            </thead>
            <tbody>
            @foreach (var item in Model.Items)
            {
                <tr>
                    <th scope="row">@item.Title</th>
                    <td><code>@item.Slug</code></td>
                    <td>@item.RevisionId</td>
                    <td>@(item.IsListed ? "Listed" : "Unlisted")</td>
                    @if (Model.MoveTargets.Count > 0)
                    {
                        <td>
                            <div class="d-flex flex-wrap gap-2">
                            @foreach (var target in Model.MoveTargets)
                            {
                                <form method="post" action="/development/databases/@item.Slug/move"
                                      onsubmit="return confirm('Move this page to @target.DisplayName and remove it from the current database?');">
                                    @Html.AntiForgeryToken()
                                    <input type="hidden" name="source" value="@Model.SelectedSourceKey" />
                                    <input type="hidden" name="targetSource" value="@target.Key" />
                                    <button class="btn btn-sm btn-outline-warning" type="submit">Move to @target.DisplayName</button>
                                </form>
                            }
                            </div>
                        </td>
                    }
                </tr>
            }
            </tbody>
        </table>
    </div>
}
