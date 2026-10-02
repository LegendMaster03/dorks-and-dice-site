using System.Net;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ServerTimingTests
{
    [Fact]
    public async Task MiddlewareAppendsPlatformTimingWithoutReplacingExistingMetrics()
    {
        var responseFeature = new RecordingResponseFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(responseFeature);

        var middleware = new ServerTimingMiddleware(nextContext =>
        {
            nextContext.Response.Headers.Append(ServerTimingMiddleware.HeaderName, "tool-db;dur=4.2");
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStartingAsync();

        var header = string.Join(',', context.Response.Headers[ServerTimingMiddleware.HeaderName].ToArray());
        Assert.Contains("tool-db;dur=4.2", header, StringComparison.Ordinal);
        Assert.Matches(@"(?:^|,)dnd-site;dur=\d+(?:\.\d{1,3})?(?:,|$)", header);
    }

    [Fact]
    public async Task MiddlewareRegistersPlatformMetricOnlyOnceWhenRequestIsReExecuted()
    {
        var responseFeature = new RecordingResponseFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(responseFeature);
        var middleware = new ServerTimingMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);
        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStartingAsync();

        var platformMetrics = context.Response.Headers[ServerTimingMiddleware.HeaderName]
            .SelectMany(value => value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? [])
            .Where(value => value.StartsWith($"{ServerTimingMiddleware.PlatformMetricName};", StringComparison.Ordinal))
            .ToArray();

        Assert.Single(platformMetrics);
    }

    [Fact]
    public async Task ToolProxyPreservesUpstreamServerTimingMetrics()
    {
        var handler = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok")
            };
            response.Headers.TryAddWithoutValidation("Server-Timing", "db;dur=12.4, app;dur=20");
            return Task.FromResult(response);
        });
        var service = CreateProxyService(handler);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("dorks-and-dice.com");
        context.Response.Body = new MemoryStream();

        await service.ProxyAsync(context, Tool("http://proxy-service:8080"), "/");

        Assert.Equal("db;dur=12.4, app;dur=20", context.Response.Headers["Server-Timing"].ToString());
    }

    [Fact]
    public async Task SiteTimingAndToolTimingAreCombinedOnProxiedResponse()
    {
        var handler = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok")
            };
            response.Headers.TryAddWithoutValidation("Server-Timing", "db;dur=12.4, app;dur=20");
            return Task.FromResult(response);
        });
        var service = CreateProxyService(handler);
        var responseFeature = new RecordingResponseFeature
        {
            Body = new MemoryStream()
        };
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(responseFeature);
        context.Request.Method = HttpMethods.Get;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("dorks-and-dice.com");

        var middleware = new ServerTimingMiddleware(nextContext =>
            service.ProxyAsync(nextContext, Tool("http://proxy-service:8080"), "/"));

        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStartingAsync();

        var header = string.Join(',', context.Response.Headers[ServerTimingMiddleware.HeaderName].ToArray());
        Assert.Contains("db;dur=12.4", header, StringComparison.Ordinal);
        Assert.Contains("app;dur=20", header, StringComparison.Ordinal);
        Assert.Matches(@"(?:^|,)dnd-site;dur=\d+(?:\.\d{1,3})?(?:,|$)", header);
    }

    private static ToolProxyService CreateProxyService(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        var configuration = new ConfigurationBuilder().Build();
        return new ToolProxyService(
            new FixedHttpClientFactory(client),
            new ToolUpstreamPolicy(configuration));
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

    private sealed class RecordingResponseFeature : IHttpResponseFeature
    {
        private readonly Stack<(Func<object, Task> Callback, object State)> _onStarting = new();

        public int StatusCode { get; set; } = StatusCodes.Status200OK;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted { get; private set; }

        public void OnStarting(Func<object, Task> callback, object state) =>
            _onStarting.Push((callback, state));

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public async Task FireOnStartingAsync()
        {
            while (_onStarting.Count > 0)
            {
                var (callback, state) = _onStarting.Pop();
                await callback(state);
            }

            HasStarted = true;
        }
    }
}
