using System.Net;
using System.Security;
using System.Threading.RateLimiting;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Resume;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.GameServers.Minecraft;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Site.ModePresentation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.Configure<MinecraftServerOptions>(
    builder.Configuration.GetSection(MinecraftServerOptions.SectionName));
builder.Services.AddSingleton<IMinecraftServerStatusService, MinecraftServerStatusService>();
builder.Services.AddContentStorage(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddScoped<IResumeContentService, ResumeContentService>();
builder.Services.AddSingleton<SiteModeOptions>();
builder.Services.AddSingleton<ISiteModePartialResolver, SiteModePartialResolver>();
builder.Services.AddSingleton<ISiteModeStylesheetResolver, SiteModeStylesheetResolver>();
builder.Services.AddSingleton<ISiteModePresentationService, SiteModePresentationService>();
builder.Services.AddSingleton<ISiteModeArchitectureSummaryService, SiteModeArchitectureSummaryService>();
builder.Services.AddSingleton<ISiteModePresentationModule, DorksAndDicePresentationModule>();
builder.Services.AddSingleton<ISiteModePresentationModule, ProfessionalPresentationModule>();
builder.Services.AddSingleton<ISiteModePresentationModule, DevelopmentPresentationModule>();
builder.Services.AddSingleton<ISiteModePresentationModule, UnassignedPresentationModule>();

builder.Services.Configure<AccountEmailOptions>(
    builder.Configuration.GetSection(AccountEmailOptions.SectionName));
builder.Services.AddScoped<IAccountEmailSender, SmtpAccountEmailSender>();

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("dorks-and-dice-site");
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

var identityStorageProvider = builder.Configuration[$"{IdentityStorageOptions.SectionName}:Provider"] ?? "PostgreSQL";
builder.Services.AddDbContext<IdentityDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = IdentityConnectionStringResolver.Resolve(configuration);

    if (string.Equals(identityStorageProvider, "Sqlite", StringComparison.OrdinalIgnoreCase)
        || string.Equals(identityStorageProvider, "SQLite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
        return;
    }

    if (string.Equals(identityStorageProvider, "PostgreSQL", StringComparison.OrdinalIgnoreCase)
        || string.Equals(identityStorageProvider, "Postgres", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString);
        return;
    }

    throw new NotSupportedException($"Identity storage provider '{identityStorageProvider}' is not supported.");
});

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = true;

        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<IdentityDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromHours(24));
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.Zero);
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();
builder.Services.AddScoped<IScopedRoleService, ScopedRoleService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IAuthorizationHandler, TrustedAccessAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, ModeScopedRoleAuthorizationHandler>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.TrustedAccess, policy =>
    {
        policy.Requirements.Add(new TrustedAccessRequirement());
    });
    options.AddPolicy(AuthorizationPolicies.AdminAccess, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(AccountRoles.Admin);
        policy.Requirements.Add(new TrustedAccessRequirement());
    });
    options.AddPolicy(AuthorizationPolicies.DevAccess, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(AccountRoles.Dev);
        policy.Requirements.Add(new TrustedAccessRequirement());
    });
    options.AddPolicy(AuthorizationPolicies.PrivilegedAccess, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(AccountRoles.Admin, AccountRoles.Dev);
        policy.Requirements.Add(new TrustedAccessRequirement());
    });
    options.AddPolicy(AuthorizationPolicies.AdminAndDevAccess, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(AccountRoles.Admin);
        policy.RequireRole(AccountRoles.Dev);
        policy.Requirements.Add(new TrustedAccessRequirement());
    });
    options.AddPolicy(AuthorizationPolicies.ModeEditor, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.Requirements.Add(new ModeScopedRoleRequirement(ScopedAccountRoles.Editor));
    });
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-dorks-and-dice.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.Path = "/";
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = true;
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/access-denied";
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("authentication", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                SegmentsPerWindow = 5,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));
});

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

using (var scope = app.Services.CreateScope())
{
    var contentStorageInitializer = scope.ServiceProvider.GetRequiredService<IContentStorageInitializer>();
    await contentStorageInitializer.InitializeAsync();
}

var applyIdentityMigrations = builder.Configuration.GetValue<bool>(
    $"{IdentityStorageOptions.SectionName}:ApplyMigrationsOnStartup");
var ensureIdentityCreated = builder.Configuration.GetValue<bool>(
    $"{IdentityStorageOptions.SectionName}:EnsureCreatedOnStartup");
if (applyIdentityMigrations && ensureIdentityCreated)
{
    throw new InvalidOperationException(
        "IdentityStorage can not enable both ApplyMigrationsOnStartup and EnsureCreatedOnStartup.");
}
if (applyIdentityMigrations || ensureIdentityCreated)
{
    using var scope = app.Services.CreateScope();
    var identityDbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    if (applyIdentityMigrations)
    {
        await identityDbContext.Database.MigrateAsync();
    }
    else
    {
        await identityDbContext.Database.EnsureCreatedAsync();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    TrustedAccessEvaluator.CaptureOriginalConnection(context);
    await next();
});
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
app.UseAuthentication();
app.UseMiddleware<SiteModeMiddleware>();
app.UseRouting();
app.UseStatusCodePagesWithReExecute("/Home/NotFoundPage");
app.UseRateLimiter();
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

app.MapPost("/development-preview", async (
    HttpContext context,
    IContentSourceRegistry contentSourceRegistry) =>
{
    var siteModeContext = context.GetSiteModeContext();
    if (!siteModeContext.HasTrustedAccess)
    {
        return Results.NotFound();
    }

    var form = await context.Request.ReadFormAsync();
    var cookieOptions = new CookieOptions
    {
        IsEssential = true,
        SameSite = SameSiteMode.Lax
    };

    if (form.ContainsKey("siteMode"))
    {
        var requestedMode = form["siteMode"].FirstOrDefault();
        if (requestedMode is not (SiteModeValues.DorksAndDiceModeValue
            or SiteModeValues.ProfessionalModeValue
            or SiteModeValues.DevelopmentModeValue))
        {
            requestedMode = SiteModeValues.DevelopmentModeValue;
        }

        context.Response.Cookies.Append(SiteModeValues.DevelopmentSiteModeCookie, requestedMode, cookieOptions);
    }

    if (form.ContainsKey("articleSettings"))
    {
        if (context.User.Identity?.IsAuthenticated != true
            || !context.User.IsInRole(AccountRoles.Dev))
        {
            return Results.Forbid();
        }

        context.Response.Cookies.Append(
            SiteModeValues.IncludeUnlistedCookie,
            form.ContainsKey("includeUnlisted") ? "true" : "false",
            cookieOptions);

        var knownSources = contentSourceRegistry.GetKnownSourceKeys();
        var enabledSources = form["enabledContentSource"]
            .Where(source => !string.IsNullOrWhiteSpace(source) && knownSources.Contains(source!))
            .Select(source => source!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var cookieValue = enabledSources.Count == 0
            ? SiteModeValues.NoContentSourcesCookieValue
            : string.Join(',', enabledSources);

        context.Response.Cookies.Append(
            SiteModeValues.EnabledContentSourcesCookie,
            cookieValue,
            cookieOptions);
    }

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
