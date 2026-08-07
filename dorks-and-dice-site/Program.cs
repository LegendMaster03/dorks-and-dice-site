using System.Net;
using System.Security;
using dorks_and_dice_site.Services.Resume;
using dorks_and_dice_site.Services.Articles;
using dorks_and_dice_site.Services.GameServers.Minecraft;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Site.ModePresentation;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.Configure<MinecraftServerOptions>(
    builder.Configuration.GetSection(MinecraftServerOptions.SectionName));
builder.Services.AddSingleton<IMinecraftServerStatusService, MinecraftServerStatusService>();
builder.Services.AddSingleton<IResumeContentService, ResumeContentService>();
builder.Services.AddSingleton<IArticleCatalogService, ArticleCatalogService>();
builder.Services.AddSingleton<SiteModeOptions>();
builder.Services.AddSingleton<ISiteModePartialResolver, SiteModePartialResolver>();
builder.Services.AddSingleton<ISiteModeStylesheetResolver, SiteModeStylesheetResolver>();
builder.Services.AddSingleton<ISiteModePresentationService, SiteModePresentationService>();
builder.Services.AddSingleton<ISiteModeArchitectureSummaryService, SiteModeArchitectureSummaryService>();
builder.Services.AddSingleton<ISiteModePresentationModule, DorksAndDicePresentationModule>();
builder.Services.AddSingleton<ISiteModePresentationModule, ProfessionalPresentationModule>();
builder.Services.AddSingleton<ISiteModePresentationModule, DevelopmentPresentationModule>();
builder.Services.AddSingleton<ISiteModePresentationModule, UnassignedPresentationModule>();

var trustedProxyAddresses = builder.Configuration
    .GetSection("ReverseProxy:KnownProxies")
    .GetChildren()
    .Select(entry => entry.Value)
    .Where(value => !string.IsNullOrWhiteSpace(value))
    .Select(value => IPAddress.Parse(value!))
    .ToArray();
var trustedProxyNetworks = builder.Configuration
    .GetSection("ReverseProxy:KnownIPNetworks")
    .GetChildren()
    .Select(entry => entry.Value)
    .Where(value => !string.IsNullOrWhiteSpace(value))
    .Select(value => System.Net.IPNetwork.Parse(value!))
    .ToArray();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedHost
        | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;

    foreach (var address in trustedProxyAddresses)
    {
        options.KnownProxies.Add(address);
    }

    foreach (var network in trustedProxyNetworks)
    {
        options.KnownIPNetworks.Add(network);
    }
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
        context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
        context.Response.Headers.TryAdd("X-Frame-Options", "SAMEORIGIN");
        return Task.CompletedTask;
    });

    await next();
});
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

app.MapGet("/robots.txt", (HttpContext context) =>
{
    var sitemapUrl = BuildAbsoluteUrl(context, "/sitemap.xml");
    return Results.Text($"User-agent: *\nAllow: /\nSitemap: {sitemapUrl}\n", "text/plain");
});

app.MapGet("/sitemap.xml", (HttpContext context) =>
{
    var siteMode = context.GetSiteModeContext().SiteMode;
    var paths = siteMode switch
    {
        dorks_and_dice_site.Models.Site.SiteMode.Professional => new[] { "/", "/resume", "/articles" },
        dorks_and_dice_site.Models.Site.SiteMode.DorksAndDice => new[] { "/", "/articles" },
        _ => new[] { "/" }
    };

    var urls = string.Join(string.Empty, paths.Select(path =>
        $"<url><loc>{SecurityElement.Escape(BuildAbsoluteUrl(context, path))}</loc></url>"));
    var xml = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">{urls}</urlset>";
    return Results.Text(xml, "application/xml");
});

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

static string BuildAbsoluteUrl(HttpContext context, string path)
{
    var siteMode = context.GetSiteModeContext().SiteMode;
    var host = siteMode == dorks_and_dice_site.Models.Site.SiteMode.Professional
        ? SiteModeOptions.CanonicalProfessionalHost
        : context.Request.Host.Value;
    return $"{context.Request.Scheme}://{host}{context.Request.PathBase}{path}";
}

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
