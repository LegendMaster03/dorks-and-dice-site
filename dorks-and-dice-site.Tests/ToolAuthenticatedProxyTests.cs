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
        context.Request.Headers[ToolAuthenticationHeaders.Ticket] = "browser-spoof";
        context.Request.Headers[ToolAuthenticationHeaders.IntrospectionPath] = "/spoofed";
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
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
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
