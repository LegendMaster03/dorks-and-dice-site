using System.Net;
using System.Text;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ToolProxyServiceTests
{
    [Fact]
    public async Task ProxyForwardsMethodPathQueryBodyAndSafeHeaders()
    {
        HttpRequestMessage? captured = null;
        var handler = new RecordingHandler(async request =>
        {
            captured = request;
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"received:{body}", Encoding.UTF8, "text/plain")
            };
        });
        var service = CreateService(handler);
        var context = CreateContext("POST", "/tools/proxy-test/api/items", "?page=2");
        context.Request.RouteValues["slug"] = "proxy-test";
        context.Request.Headers["X-Custom"] = "forward-me";
        context.Request.Headers["Cookie"] = "site-auth=secret";
        context.Request.Headers["Authorization"] = "Bearer secret";
        var bodyBytes = Encoding.UTF8.GetBytes("payload");
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.ContentLength = bodyBytes.Length;

        await service.ProxyAsync(context, Tool("http://proxy-service:8080"), "/api/items");

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("http://proxy-service:8080/api/items?page=2", captured.RequestUri?.ToString());
        Assert.True(captured.Headers.TryGetValues("X-Custom", out var custom));
        Assert.Contains("forward-me", custom!);
        Assert.False(captured.Headers.Contains("Cookie"));
        Assert.Null(captured.Headers.Authorization);
        Assert.True(captured.Headers.TryGetValues("X-Forwarded-Prefix", out var prefix));
        Assert.Contains("/tools/proxy-test", prefix!);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        Assert.Equal("received:payload", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task ProxyDoesNotExposeUpstreamCookies()
    {
        var handler = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok")
            };
            response.Headers.TryAddWithoutValidation("Set-Cookie", "upstream=session");
            return Task.FromResult(response);
        });
        var service = CreateService(handler);
        var context = CreateContext("GET", "/tools/proxy-test", string.Empty);
        context.Request.RouteValues["slug"] = "proxy-test";

        await service.ProxyAsync(context, Tool("http://proxy-service:8080"), "/");

        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProxyRejectsUpstreamRedirects()
    {
        var handler = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new Uri("http://proxy-service:8080/login");
            return Task.FromResult(response);
        });
        var service = CreateService(handler);
        var context = CreateContext("GET", "/tools/proxy-test", string.Empty);
        context.Request.RouteValues["slug"] = "proxy-test";

        await service.ProxyAsync(context, Tool("http://proxy-service:8080"), "/");

        Assert.Equal(StatusCodes.Status502BadGateway, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
    }

    [Fact]
    public async Task ProxyRejectsDisallowedUpstreamBeforeNetworkCall()
    {
        var called = false;
        var handler = new RecordingHandler(_ =>
        {
            called = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var service = CreateService(handler);
        var context = CreateContext("GET", "/tools/proxy-test", string.Empty);
        context.Request.RouteValues["slug"] = "proxy-test";

        await service.ProxyAsync(context, Tool("https://google.com"), "/");

        Assert.False(called);
        Assert.Equal(StatusCodes.Status502BadGateway, context.Response.StatusCode);
    }

    private static ToolProxyService CreateService(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        var factory = new FixedHttpClientFactory(client);
        var configuration = new ConfigurationBuilder().Build();
        return new ToolProxyService(factory, new ToolUpstreamPolicy(configuration));
    }

    private static DefaultHttpContext CreateContext(string method, string path, string query)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("dorks-and-dice.com");
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static ToolRegistration Tool(string upstream) => new()
    {
        Slug = "proxy-test",
        IntegrationType = ToolIntegrationType.ProxiedApplication,
        UpstreamBaseUrl = upstream,
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
