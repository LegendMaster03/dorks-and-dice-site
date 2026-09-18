using System.Net;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ToolAuthenticatedProxyTests
{
    [Fact]
    public async Task AuthenticatedProxyReplacesBrowserSpoofedAuthHeadersWithTrustedTicket()
    {
        HttpRequestMessage? captured = null;
        var handler = new RecordingHandler(request =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok")
            });
        });
        var service = CreateService(handler);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("dorks-and-dice.com");
        context.Request.Headers.Authorization = "Bearer ddop_v1_must-not-reach-tool";
        context.Request.Headers[ToolAuthenticationHeaders.Ticket] = "browser-spoof";
        context.Request.Headers[ToolAuthenticationHeaders.IntrospectionPath] = "/spoofed";
        context.Request.Headers[ToolLifecycleHeaders.Ticket] = "browser-lifecycle-spoof";
        context.Request.Headers[ToolLifecycleHeaders.IntrospectionPath] = "/spoofed-lifecycle";
        context.Request.Headers[ToolDelegationHeaders.Capability] = "browser-delegation-spoof";
        context.Request.Headers[ToolDelegationHeaders.Path] = "/spoofed-delegation";
        context.Response.Body = new MemoryStream();

        await service.ProxyAuthenticatedAsync(
            context,
            Tool(),
            "/api/rules",
            "trusted-ticket",
            "/tool-host/rules-core/api/introspect");

        Assert.NotNull(captured);
        Assert.Equal(
            "trusted-ticket",
            captured!.Headers.GetValues(ToolAuthenticationHeaders.Ticket).Single());
        Assert.Equal(
            "/tool-host/rules-core/api/introspect",
            captured.Headers.GetValues(ToolAuthenticationHeaders.IntrospectionPath).Single());
        Assert.False(captured.Headers.Contains(ToolLifecycleHeaders.Ticket));
        Assert.False(captured.Headers.Contains(ToolLifecycleHeaders.IntrospectionPath));
        Assert.False(captured.Headers.Contains(ToolDelegationHeaders.Capability));
        Assert.False(captured.Headers.Contains(ToolDelegationHeaders.Path));
        Assert.False(captured.Headers.Contains("Authorization"));
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task ProxyDoesNotExposeReservedDelegationHeadersFromUpstream()
    {
        var handler = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok")
            };
            response.Headers.TryAddWithoutValidation(
                ToolDelegationHeaders.Capability,
                "must-not-reach-browser");
            response.Headers.TryAddWithoutValidation(
                ToolAuthenticationHeaders.Ticket,
                "must-not-reach-browser");
            return Task.FromResult(response);
        });
        var service = CreateService(handler);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("dorks-and-dice.com");
        context.Response.Body = new MemoryStream();

        await service.ProxyAsync(context, Tool(), "/api/rules");

        Assert.False(context.Response.Headers.ContainsKey(ToolDelegationHeaders.Capability));
        Assert.False(context.Response.Headers.ContainsKey(ToolAuthenticationHeaders.Ticket));
    }

    [Fact]
    public async Task AnonymousProxyStripsBrowserSuppliedLifecycleHeaders()
    {
        HttpRequestMessage? captured = null;
        var handler = new RecordingHandler(request =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var service = CreateService(handler);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("dorks-and-dice.com");
        context.Request.Headers[ToolLifecycleHeaders.Ticket] = "browser-lifecycle-spoof";
        context.Request.Headers[ToolLifecycleHeaders.IntrospectionPath] = "/spoofed-lifecycle";
        context.Response.Body = new MemoryStream();

        await service.ProxyAsync(context, Tool(), "/api/rules");

        Assert.NotNull(captured);
        Assert.False(captured!.Headers.Contains(ToolLifecycleHeaders.Ticket));
        Assert.False(captured.Headers.Contains(ToolLifecycleHeaders.IntrospectionPath));
    }

    private static ToolProxyService CreateService(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        var factory = new FixedHttpClientFactory(client);
        var configuration = new ConfigurationBuilder().Build();
        return new ToolProxyService(factory, new ToolUpstreamPolicy(configuration));
    }

    private static ToolRegistration Tool() => new()
    {
        Slug = "rules-core",
        IntegrationType = ToolIntegrationType.EmbeddedModule,
        UpstreamBaseUrl = "http://rules-core:8080",
        Enabled = true
    };

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responder(request);
    }
}
