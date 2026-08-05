using dorks_and_dice_site.Services.Resume;
using dorks_and_dice_site.Services.Articles;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Site.ModePresentation;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IResumeContentService, ResumeContentService>();
builder.Services.AddSingleton<IArticleCatalogService, ArticleCatalogService>();
builder.Services.AddSingleton<SiteModeOptions>();
builder.Services.AddSingleton<ISiteModePartialResolver, SiteModePartialResolver>();
builder.Services.AddSingleton<ISiteModePresentationService, SiteModePresentationService>();
builder.Services.AddSingleton<ISiteModePresentationModule, DorksAndDicePresentationModule>();
builder.Services.AddSingleton<ISiteModePresentationModule, ProfessionalPresentationModule>();
builder.Services.AddSingleton<ISiteModePresentationModule, DevelopmentPresentationModule>();
builder.Services.AddSingleton<ISiteModePresentationModule, UnassignedPresentationModule>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedHost
        | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    var requestedHost = context.Request.Host.Host.ToLowerInvariant();
    var normalizedHost = NormalizeHost(requestedHost);
    var options = context.RequestServices.GetRequiredService<SiteModeOptions>();
    if (options.ProfessionalDomains.Contains(normalizedHost)
        && !string.Equals(requestedHost, SiteModeOptions.CanonicalProfessionalHost, StringComparison.OrdinalIgnoreCase))
    {
        var target = $"https://{SiteModeOptions.CanonicalProfessionalHost}{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
        context.Response.Redirect(target, permanent: true, preserveMethod: true);
        return;
    }

    await next();
});
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
    if (string.IsNullOrWhiteSpace(returnUrl) || !IsLocalUrl(returnUrl))
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

static string NormalizeHost(string host)
{
    var normalizedHost = host.ToLowerInvariant();
    return normalizedHost.StartsWith("www.", StringComparison.Ordinal)
        ? normalizedHost[4..]
        : normalizedHost;
}

static bool IsLocalUrl(string url)
{
    if (string.IsNullOrEmpty(url))
    {
        return false;
    }

    if (url[0] == '/')
    {
        return url.Length == 1 || (url[1] != '/' && url[1] != '\\');
    }

    return url.Length > 1
        && url[0] == '~'
        && url[1] == '/'
        && (url.Length == 2 || (url[2] != '/' && url[2] != '\\'));
}

public partial class Program
{
}
