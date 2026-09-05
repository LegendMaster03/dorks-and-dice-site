using System.Net;
using System.Text.Json;
using dorks_and_dice_site.Models.Campaigns;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Campaigns;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class ToolHostingClosureTests(PublishedContentWebApplicationFactory factory)
{
    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "?q=a%2Fb&x=1&x=2")]
    [InlineData("HEAD", "?q=test")]
    public async Task ProxiedRootRedirectsOncePreservingQueryAndRelativeLinks(string method, string query)
    {
        var requests = new List<Uri>();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddHttpClient(ToolHttpClientNames.Proxy).ConfigurePrimaryHttpMessageHandler(() =>
                new UpstreamHandler(request =>
                {
                    requests.Add(request.RequestUri!);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("<a href=\"nested/page\">Next</a>")
                    };
                }))));
        var tool = await RegisterAsync(ToolIntegrationType.ProxiedApplication);
        try
        {
            using var client = Client(host);
            using var first = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), $"/tools/{tool.Slug}{query}"));
            Assert.Equal(HttpStatusCode.TemporaryRedirect, first.StatusCode);
            Assert.Equal($"/tools/{tool.Slug}/{query}", first.Headers.Location?.OriginalString);
            Assert.Empty(requests);
            var canonical = new Uri(client.BaseAddress!, first.Headers.Location!);
            using var root = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), canonical));
            Assert.Equal(HttpStatusCode.OK, root.StatusCode);
            Assert.Null(root.Headers.Location);
            Assert.Equal("/base/", Assert.Single(requests).AbsolutePath);
            Assert.Equal(query, requests[0].Query);
            if (method == "GET")
            {
                var html = await root.Content.ReadAsStringAsync();
                Assert.Contains("href=\"nested/page\"", html);
                var nestedUrl = new Uri(canonical, "nested/page");
                Assert.Equal($"/tools/{tool.Slug}/nested/page", nestedUrl.AbsolutePath);
                using var nested = await client.GetAsync(nestedUrl);
                Assert.Equal(HttpStatusCode.OK, nested.StatusCode);
                Assert.Equal("/base/nested/page", requests[1].AbsolutePath);
            }
            // A separate direct request must remain stable, too.
            using var repeated = await client.GetAsync(canonical);
            Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
            Assert.Null(repeated.Headers.Location);
        }
        finally { await Registry.DeleteAsync(tool.Id); }
    }

    [Theory]
    [InlineData("tool-proxy")]
    [InlineData("tool-hosting")]
    public void PooledUpstreamHandlersDoNotStoreCookiesOrFollowRedirects(string clientName)
    {
        var handler = factory.Services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(clientName);
        while (handler is DelegatingHandler delegating) handler = delegating.InnerHandler!;
        var primary = Assert.IsType<HttpClientHandler>(handler);
        Assert.False(primary.UseCookies);
        Assert.False(primary.AllowAutoRedirect);
        Assert.Equal(DecompressionMethods.None, primary.AutomaticDecompression);
    }

    [Theory]
    [InlineData("session")]
    [InlineData("campaigns")]
    [InlineData("campaigns/00000000-0000-0000-0000-000000000001")]
    public async Task EveryAuthoritativeApiRejectsAnonymousIdentitySpoofing(string endpoint)
    {
        var tool = await RegisterAsync();
        try
        {
            using var client = Client(factory);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/tool-host/{tool.Slug}/api/{endpoint}?userId=integration-test-user");
            request.Headers.Add("X-User-Id", "integration-test-user");
            request.Headers.Add("X-Forwarded-User", "integration-test-user");
            request.Headers.Authorization = new("Bearer", "browser-spoof");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally { await Registry.DeleteAsync(tool.Id); }
    }

    [Theory]
    [InlineData(false, false, true, 404)]
    [InlineData(true, true, true, 404)]
    [InlineData(true, false, false, 401)]
    public async Task ToolEntryPointsEnforceAvailabilityBeforeUpstream(bool enabled, bool wrongMode, bool allowAnonymous, int status)
    {
        var tool = await RegisterAsync(ToolIntegrationType.ProxiedApplication);
        tool.Enabled = enabled;
        tool.AllowAnonymous = allowAnonymous;
        if (wrongMode) tool.Modes = [SiteModeValues.ProfessionalModeValue];
        await Registry.SaveAsync(tool);
        try
        {
            using var client = Client(factory);
            foreach (var path in new[] { $"/tools/{tool.Slug}", $"/tools/{tool.Slug}/nested", $"/tool-host/{tool.Slug}/context" })
            {
                using var response = await client.GetAsync(path);
                Assert.Equal(status, (int)response.StatusCode);
            }
            if (status == 404)
            {
                client.DefaultRequestHeaders.Add(TestRoleAuthenticationHandler.RolesHeader, "Member");
                foreach (var endpoint in new[] { "session", "campaigns", $"campaigns/{Guid.NewGuid()}" })
                {
                    using var response = await client.GetAsync($"/tool-host/{tool.Slug}/api/{endpoint}");
                    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                }
            }
            tool.IntegrationType = ToolIntegrationType.EmbeddedModule;
            await Registry.SaveAsync(tool);
            client.DefaultRequestHeaders.Remove(TestRoleAuthenticationHandler.RolesHeader);
            using var module = await client.GetAsync($"/tool-modules/{tool.Slug}/app.js");
            Assert.Equal(status, (int)module.StatusCode);
        }
        finally { await Registry.DeleteAsync(tool.Id); }
    }

    [Fact]
    public async Task CampaignApiUsesMembershipAndConcealsOtherCampaigns()
    {
        var tool = await RegisterAsync();
        var store = factory.Services.GetRequiredService<ICampaignAccessStore>();
        var member = new CampaignRecord { Id = Guid.NewGuid(), Name = "Member campaign" };
        var other = new CampaignRecord { Id = Guid.NewGuid(), Name = "Private campaign" };
        await store.SaveCampaignAsync(member);
        await store.SaveCampaignAsync(other);
        await store.SaveMembershipAsync(new() { CampaignId = member.Id, UserId = "integration-test-user", Role = CampaignRoles.Player });
        await store.SaveMembershipAsync(new() { CampaignId = other.Id, UserId = "spoofed", Role = CampaignRoles.Dm });
        try
        {
            using var client = Client(factory);
            client.DefaultRequestHeaders.Add(TestRoleAuthenticationHandler.RolesHeader, "Member");
            client.DefaultRequestHeaders.Add("X-User-Id", "spoofed");
            var prefix = $"/tool-host/{tool.Slug}/api/campaigns";
            using var list = await client.GetAsync(prefix + "?userId=spoofed");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            Assert.Equal("no-store", list.Headers.CacheControl?.ToString());
            using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
            var summary = Assert.Single(listJson.RootElement.EnumerateArray());
            Assert.Equal(member.Id, summary.GetProperty("id").GetGuid());
            Assert.Equal("Player", summary.GetProperty("role").GetString());
            Assert.Equal(3, summary.EnumerateObject().Count());
            using var detail = await client.GetAsync($"{prefix}/{member.Id}?userId=spoofed");
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            Assert.Equal("no-store", detail.Headers.CacheControl?.ToString());
            using var denied = await client.GetAsync($"{prefix}/{other.Id}");
            using var missing = await client.GetAsync($"{prefix}/{Guid.NewGuid()}");
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            Assert.Equal(missing.StatusCode, denied.StatusCode);
            // The shared error-page shell includes a fresh antiforgery token per response.
            var missingHtml = await missing.Content.ReadAsStringAsync();
            var deniedHtml = await denied.Content.ReadAsStringAsync();
            Assert.DoesNotContain(other.Name, deniedHtml);
            Assert.Equal(
                System.Text.RegularExpressions.Regex.Replace(missingHtml, "value=\"[^\"]*\"", "value=\"\""),
                System.Text.RegularExpressions.Regex.Replace(deniedHtml, "value=\"[^\"]*\"", "value=\"\""));
            Assert.True(denied.Headers.CacheControl?.NoStore);
            using var write = await client.PostAsync(prefix, new StringContent("{}"));
            Assert.Equal(HttpStatusCode.MethodNotAllowed, write.StatusCode);
        }
        finally
        {
            await store.DeleteCampaignAsync(member.Id);
            await store.DeleteCampaignAsync(other.Id);
            await Registry.DeleteAsync(tool.Id);
        }
    }

    [Theory]
    [InlineData("Dev", "localhost", 200)]
    [InlineData("Admin", "localhost", 403)]
    [InlineData("Member", "localhost", 403)]
    [InlineData("Dev", "dorks-and-dice.com", 403)]
    public async Task ManagementRequiresDevAndTrustedAccess(string role, string host, int expected)
    {
        using var client = Client(factory, host);
        client.DefaultRequestHeaders.Add(TestRoleAuthenticationHandler.RolesHeader, role);
        client.DefaultRequestHeaders.Add(TestRoleAuthenticationHandler.ScopedRolesHeader, "dorks-and-dice:Editor");
        using var response = await client.GetAsync("/development/tools/new");
        Assert.Equal(expected, (int)response.StatusCode);
        using var save = await client.PostAsync("/development/tools/save", new FormUrlEncodedContent([]));
        // Authorized mutation still requires antiforgery; unauthorized accounts never get there.
        Assert.Equal(expected == 200 ? 400 : expected, (int)save.StatusCode);
    }

    [Fact]
    public async Task OwnerReceivesDevThroughProductionClaimsFactory()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roles.RoleExistsAsync(AccountRoles.Owner))
            Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(AccountRoles.Owner))).Succeeded);
        var email = $"closure-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, DisplayName = "Closure Owner" };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        try
        {
            Assert.True((await users.AddToRoleAsync(user, AccountRoles.Owner)).Succeeded);
            var principal = await scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>().CreateAsync(user);
            Assert.True(principal.IsInRole(AccountRoles.Dev));
            using var client = Client(factory, "localhost");
            client.DefaultRequestHeaders.Add(TestRoleAuthenticationHandler.RolesHeader,
                string.Join(",", principal.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value)));
            using var response = await client.GetAsync("/development/tools/new");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally { await users.DeleteAsync(user); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmbeddedModulesPreserveShellDiscoveryAndRelativeImports(bool compressed)
    {
        var requests = new List<Uri>();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddHttpClient(ToolHttpClientNames.Hosting).ConfigurePrimaryHttpMessageHandler(() =>
                new UpstreamHandler(request =>
                {
                    requests.Add(request.RequestUri!);
                    Assert.False(request.Headers.Contains("Cookie"));
                    Assert.Null(request.Headers.Authorization);
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("import './lib/helper.js'; document.getElementById('tool-root').textContent = 'Ready';",
                            System.Text.Encoding.UTF8, "text/javascript")
                    };
                    if (compressed)
                    {
                        using var buffer = new MemoryStream();
                        using (var gzip = new System.IO.Compression.GZipStream(buffer, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
                        {
                            gzip.Write(System.Text.Encoding.UTF8.GetBytes("import './lib/helper.js';"));
                        }
                        response.Content = new ByteArrayContent(buffer.ToArray());
                        response.Content.Headers.ContentType = new("text/javascript");
                        response.Content.Headers.ContentEncoding.Add("gzip");
                        response.Headers.Vary.Add("Accept-Encoding");
                    }
                    response.Headers.TryAddWithoutValidation("Set-Cookie", "module=secret");
                    return response;
                }))));
        var tool = await RegisterAsync();
        tool.FrontendEntryPoint = "/app.js";
        await Registry.SaveAsync(tool);
        try
        {
            using var client = Client(host);
            client.DefaultRequestHeaders.Add("Cookie", "__Host-dorks-and-dice.auth=secret");
            client.DefaultRequestHeaders.Authorization = new("Bearer", "secret");
            using var page = await client.GetAsync($"/tools/{tool.Slug}");
            var html = await page.Content.ReadAsStringAsync();
            Assert.Contains("<html", html);
            Assert.Contains($"data-tool-context-url=\"/tool-host/{tool.Slug}/context\"", html);
            Assert.Contains("await import(moduleUrl)", html);
            Assert.Contains("Tool unavailable", html);
            var moduleUrl = new Uri(client.BaseAddress!, $"/tool-modules/{tool.Slug}/app.js");
            using var module = await client.GetAsync(moduleUrl);
            Assert.Equal(HttpStatusCode.OK, module.StatusCode);
            Assert.Equal("text/javascript", module.Content.Headers.ContentType?.MediaType);
            if (compressed)
            {
                Assert.Contains("gzip", module.Content.Headers.ContentEncoding);
                Assert.Contains("Accept-Encoding", module.Headers.Vary);
                await using var body = await module.Content.ReadAsStreamAsync();
                await using var gzip = new System.IO.Compression.GZipStream(body, System.IO.Compression.CompressionMode.Decompress);
                using var reader = new StreamReader(gzip);
                Assert.Contains("import './lib/helper.js'", await reader.ReadToEndAsync());
            }
            else
            {
                Assert.Contains("import './lib/helper.js'", await module.Content.ReadAsStringAsync());
            }
            Assert.False(module.Headers.Contains("Set-Cookie"));
            using var helper = await client.GetAsync(new Uri(moduleUrl, "./lib/helper.js"));
            Assert.Equal(HttpStatusCode.OK, helper.StatusCode);
            Assert.Equal("/base/lib/helper.js", requests.Last().AbsolutePath);
        }
        finally { await Registry.DeleteAsync(tool.Id); }
    }

    private IToolRegistry Registry => factory.Services.GetRequiredService<IToolRegistry>();

    [Fact]
    public async Task EmptyManagementFieldsReturnValidationInsteadOfServerError()
    {
        using var client = Client(factory, "localhost");
        client.DefaultRequestHeaders.Add(TestRoleAuthenticationHandler.RolesHeader, "Dev");
        var html = await client.GetStringAsync("/development/tools/new");
        var match = System.Text.RegularExpressions.Regex.Match(html,
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(match.Success);
        using var response = await client.PostAsync("/development/tools/save",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value),
                ["Slug"] = "", ["DisplayName"] = "", ["DorksAndDiceMode"] = "true"
            }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsStringAsync();
        Assert.Contains("field-validation-error", result);
        Assert.Contains("required", result);
    }

    private async Task<ToolRegistration> RegisterAsync(ToolIntegrationType integration = ToolIntegrationType.EmbeddedModule)
    {
        var tool = new ToolRegistration
        {
            Id = Guid.NewGuid(), Slug = $"closure-{Guid.NewGuid():N}", DisplayName = "Closure Tool",
            Enabled = true, AllowAnonymous = true, IntegrationType = integration,
            UpstreamBaseUrl = "http://closure-service:8080/base"
        };
        await Registry.SaveAsync(tool);
        return tool;
    }

    private static HttpClient Client(WebApplicationFactory<Program> host, string name = "dorks-and-dice.com") =>
        host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false, BaseAddress = new Uri($"https://{name}")
        });

    private sealed class UpstreamHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
