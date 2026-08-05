using dorks_and_dice_site.Services.Resume;
using dorks_and_dice_site.Services.Articles;
using dorks_and_dice_site.Services.Site;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IResumeContentService, ResumeContentService>();
builder.Services.AddSingleton<IArticleCatalogService, ArticleCatalogService>();
builder.Services.AddSingleton<SiteModeOptions>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<SiteModeMiddleware>();
app.UseRouting();
app.UseStatusCodePagesWithReExecute("/Home/NotFoundPage");

app.UseAuthorization();

app.MapStaticAssets();

app.MapGet("/health", () => Results.Text("OK", "text/plain"));

app.MapPost("/development-preview", async (HttpContext context) =>
{
    var siteModeContext = context.GetSiteModeContext();
    if (!siteModeContext.IsDevelopmentPreview)
    {
        return Results.NotFound();
    }

    var form = await context.Request.ReadFormAsync();
    var requestedMode = form["siteMode"].FirstOrDefault();
    if (requestedMode is not (SiteModeValues.DorksAndDiceModeValue
        or SiteModeValues.ProfessionalModeValue
        or SiteModeValues.DevelopmentModeValue))
    {
        requestedMode = SiteModeValues.DevelopmentModeValue;
    }

    var includeUnlisted = form.ContainsKey("includeUnlisted");
    var cookieOptions = new CookieOptions
    {
        IsEssential = true,
        SameSite = SameSiteMode.Lax
    };

    context.Response.Cookies.Append(SiteModeValues.DevelopmentSiteModeCookie, requestedMode, cookieOptions);
    context.Response.Cookies.Append(SiteModeValues.IncludeUnlistedCookie, includeUnlisted ? "true" : "false", cookieOptions);

    var returnUrl = form["returnUrl"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith("/", StringComparison.Ordinal))
    {
        returnUrl = "/";
    }

    return Results.Redirect(returnUrl);
});

app.MapControllerRoute(
    name: "resume",
    pattern: "resume",
    defaults: new { controller = "Resume", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
