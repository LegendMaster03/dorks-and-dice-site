using dorks_and_dice_site.Services.Identity;
using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContentMediaExternalPromotion;

internal sealed class ReadOnlyRegistry(IContentSourceRegistry original) : IContentSourceRegistry
{
    public string AuthoringSourceKey => original.AuthoringSourceKey;
    private static ContentSourceDefinition Protect(ContentSourceDefinition source) => source with { ConnectionString = new Database(source.Provider, source.ConnectionString).ReadOnlyConnectionString };
    public ContentSourceDefinition GetSource(string key) => Protect(original.GetSource(key));
    public IReadOnlyList<ContentSourceDefinition> GetAllSources() => original.GetAllSources().Select(Protect).ToList();
    public IReadOnlyList<ContentSourceDefinition> GetGlobalSources() => original.GetGlobalSources().Select(Protect).ToList();
    public bool IsGlobalSource(string key) => original.IsGlobalSource(key);
    public IReadOnlySet<string> GetKnownSourceKeys() => original.GetKnownSourceKeys();
    public IReadOnlyList<ContentSourceDefinition> GetSourcesByKeys(IEnumerable<string> keys) => original.GetSourcesByKeys(keys).Select(Protect).ToList();
    public IReadOnlyList<ContentSourceDefinition> GetDefaultSources(string modeId) => original.GetDefaultSources(modeId).Select(Protect).ToList();
    public IReadOnlyList<ContentSourceDefinition> GetDefaultSources(SiteMode mode) => original.GetDefaultSources(mode).Select(Protect).ToList();
    public void ConfigureDbContext(DbContextOptionsBuilder options, string sourceKey)
    { var source = GetSource(sourceKey); if (source.Provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase)) options.UseSqlite(source.ConnectionString); else options.UseNpgsql(source.ConnectionString); }
}
internal sealed class NoSchemaWrites : IContentStorageInitializer
{ public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask; }
internal sealed class VerificationAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string NameOfScheme = "PromotionVerificationOnly";
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-Promotion-Developer")) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "promotion-verification"), new(ClaimTypes.Role, AccountRoles.Dev)], NameOfScheme);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), NameOfScheme)));
    }
}
internal sealed class VerificationHost(string root, string state, IContentSourceRegistry registry) : WebApplicationFactory<ContentAssetService>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development"); builder.UseContentRoot(Path.Combine(root, "dorks-and-dice-site"));
        builder.UseSetting("IdentityStorage:ApplyMigrationsOnStartup", "false"); builder.UseSetting("IdentityStorage:EnsureCreatedOnStartup", "false");
        builder.UseSetting("DataProtection:KeysPath", Path.Combine(state, "verification-keys"));
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            var readOnly = new ReadOnlyRegistry(registry);
            services.RemoveAll<IContentSourceRegistry>(); services.AddSingleton<IContentSourceRegistry>(readOnly);
            services.RemoveAll<DbContextOptions<ContentDbContext>>(); services.RemoveAll<ContentDbContext>();
            services.AddDbContext<ContentDbContext>(options => readOnly.ConfigureDbContext(options, readOnly.AuthoringSourceKey));
            services.RemoveAll<IContentStorageInitializer>(); services.AddSingleton<IContentStorageInitializer, NoSchemaWrites>();
            services.AddAuthentication(options => { options.DefaultAuthenticateScheme = VerificationAuthentication.NameOfScheme; options.DefaultChallengeScheme = VerificationAuthentication.NameOfScheme; })
                .AddScheme<AuthenticationSchemeOptions, VerificationAuthentication>(VerificationAuthentication.NameOfScheme, _ => { });
        });
    }
}
internal static class HttpVerification
{
    public static async Task<int> Run(string root, string state, IContentSourceRegistry registry, IConfiguration configuration, PromotionPlan plan, Journal journal)
    {
        using var host = new VerificationHost(root, state, registry);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        int checks = 0;
        async Task<HttpResponseMessage> Get(string path, string source, string mode = "professional", bool developer = true, string requestHost = "localhost")
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{requestHost}{path}");
            request.Headers.Host = requestHost;
            if (developer) request.Headers.Add("X-Promotion-Developer", "true");
            request.Headers.Add("Cookie", $"DevelopmentPreviewSiteMode={mode}; DevelopmentEnabledContentSources={source}");
            return await client.SendAsync(request);
        }
        foreach (var item in plan.Entries.Where(e => e.Applicable))
        {
            var staged = journal.Assets[item.Manifest.LocalAssetKey];
            using var response = await Get(staged.Url, "External");
            if (response.StatusCode != HttpStatusCode.OK || StateStore.Sha(await response.Content.ReadAsByteArrayAsync()) != item.Sha256) throw new PromotionException($"External HTTP media verification failed: {item.Manifest.Slug}.");
            checks++;
            var baseline = plan.Pages[item.Manifest.Slug];
            bool dorksVisible = baseline.History["content_revision_mode"].Any(r => r["revision_id"] == baseline.Current["revision_id"] && r["site_mode"] is "dorks-and-dice" or "DorksAndDice");
            if (!dorksVisible)
            {
                using var isolated = await Get(staged.Url, "External", "dorks-and-dice", false, configuration["SiteHosting:Modes:dorks-and-dice:CanonicalHost"] ?? "dorks-and-dice.com");
                if (isolated.StatusCode != HttpStatusCode.NotFound) throw new PromotionException("Professional media leaked into Dorks & Dice.");
                checks++;
            }
            if (staged.ExternalKey != item.Manifest.LocalAssetKey)
            {
                using var localOnly = await Get(staged.Url, "Local");
                if (localOnly.StatusCode != HttpStatusCode.NotFound) throw new PromotionException("Local-only selection unexpectedly depends on promoted External media.");
                checks++;
            }
        }
        foreach (var item in plan.Entries)
        {
            var url = $"/content/media/{item.Manifest.LocalAssetKey}/{item.FileName}";
            using var response = await Get(url, "Local");
            if (response.StatusCode != HttpStatusCode.OK || StateStore.Sha(await response.Content.ReadAsByteArrayAsync()) != item.Sha256) throw new PromotionException("Local-only media behavior changed.");
            checks++;
        }
        foreach (var (slug, baseline) in plan.Pages)
        {
            var tags = baseline.History["content_revision_tag"].Where(r => r["revision_id"] == baseline.Current["revision_id"]).Select(r => r["tag"]).ToHashSet();
            var path = slug.EndsWith("-home", StringComparison.Ordinal) ? "/resume" : tags.Contains("article") ? "/articles/" + slug : "/resume/" + slug;
            using var response = await Get(path, "External"); var html = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != HttpStatusCode.OK) throw new PromotionException($"External page did not render: {slug}.");
            foreach (var entry in plan.Entries.Where(e => e.Applicable && e.Manifest.Slug == slug))
                if (!html.Contains(journal.Assets[entry.Manifest.LocalAssetKey].Url, StringComparison.Ordinal)) throw new PromotionException($"Managed reference missing from rendered page: {slug}.");
            checks++;
        }
        foreach (var source in new[] { "Local", "External", "External%2CLocal" })
        foreach (var mode in new[] { "professional", "dorks-and-dice" })
        {
            using var response = await Get("/", source, mode);
            if (response.StatusCode != HttpStatusCode.OK) throw new PromotionException($"Homepage verification failed: mode={mode}, sources={source}, HTTP={(int)response.StatusCode}; {checks} earlier HTTP checks passed.");
            checks++;
        }
        return checks;
    }
    public static async Task RunApplicationTests(string root, string state)
    {
        // These are the normal application's isolated fixtures, never a migration write to live External.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CONTENT_TEST_POSTGRES")) || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES")))
            throw new PromotionException("Unset integration-test database overrides before running verification tests.");
        var info = new ProcessStartInfo("dotnet") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "test", "dorks-and-dice-site.slnx", "-c", "Release", "-p:UseAppHost=false", "--results-directory", Path.Combine(state, "test-results"), "--logger", "trx;LogFileName=application.trx" }) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new PromotionException("Could not start normal test suite.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await File.WriteAllTextAsync(Path.Combine(state, "application-tests.log"), await output + await error);
        if (process.ExitCode != 0) throw new PromotionException("Normal application tests failed; verification is incomplete. Inspect application-tests.log in the ignored state directory.");
    }
}
