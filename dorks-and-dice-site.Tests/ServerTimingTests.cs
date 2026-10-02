using System.Globalization;
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

        var metrics = ReadMetricEntries(context);
        Assert.Contains(metrics, metric => metric.Name == "tool-db" && metric.DurationMilliseconds == 4.2);
        Assert.Single(metrics.Where(metric => metric.Name == ServerTimingMiddleware.SiteMetricName));
        Assert.Single(metrics.Where(metric => metric.Name == ServerTimingMiddleware.TotalMetricName));
        Assert.DoesNotContain(metrics, metric => metric.Name == ServerTimingMiddleware.ToolMetricName);
    }

    [Fact]
    public async Task MiddlewareRegistersPlatformMetricsOnlyOnceWhenRequestIsReExecuted()
    {
        var responseFeature = new RecordingResponseFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(responseFeature);
        var middleware = new ServerTimingMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);
        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStartingAsync();

        var metrics = ReadMetricEntries(context);
        Assert.Single(metrics.Where(metric => metric.Name == ServerTimingMiddleware.SiteMetricName));
        Assert.Single(metrics.Where(metric => metric.Name == ServerTimingMiddleware.TotalMetricName));
    }

    [Fact]
    public async Task ToolProxyPreservesAndAppendsUpstreamServerTimingMetrics()
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
        context.Response.Headers.Append("Server-Timing", "existing;dur=1.5");

        await service.ProxyAsync(context, Tool("http://proxy-service:8080"), "/");

        var header = context.Response.Headers["Server-Timing"].ToString();
        Assert.Contains("existing;dur=1.5", header, StringComparison.Ordinal);
        Assert.Contains("db;dur=12.4", header, StringComparison.Ordinal);
        Assert.Contains("app;dur=20", header, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SiteDistinguishesSiteTimeFromNamedToolWaitWithoutToolInstrumentation()
    {
        var handler = new RecordingHandler(async _ =>
        {
            await Task.Delay(20);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok")
            };
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

        var metrics = ReadMetricEntries(context);
        var site = Assert.Single(metrics.Where(metric => metric.Name == ServerTimingMiddleware.SiteMetricName));
        var tool = Assert.Single(metrics.Where(metric => metric.Name == ServerTimingMiddleware.ToolMetricName));
        var total = Assert.Single(metrics.Where(metric => metric.Name == ServerTimingMiddleware.TotalMetricName));

        Assert.Equal("proxy-test", tool.Description);
        Assert.True(tool.DurationMilliseconds >= 10);
        Assert.True(total.DurationMilliseconds >= tool.DurationMilliseconds);
        Assert.True(site.DurationMilliseconds < total.DurationMilliseconds);
    }

    [Fact]
    public async Task SiteTimingAndToolOwnedTimingAreCombinedOnProxiedResponse()
    {
        var handler = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok")
            };
            response.Headers.TryAddWithoutValidation("Server-Timing", "rules-core;dur=20, rules-core-db;dur=12.4");
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

        var metrics = ReadMetricEntries(context);
        Assert.Contains(metrics, metric => metric.Name == "rules-core" && metric.DurationMilliseconds == 20);
        Assert.Contains(metrics, metric => metric.Name == "rules-core-db" && metric.DurationMilliseconds == 12.4);
        Assert.Contains(metrics, metric => metric.Name == ServerTimingMiddleware.SiteMetricName);
        Assert.Contains(metrics, metric =>
            metric.Name == ServerTimingMiddleware.ToolMetricName
            && metric.Description == "proxy-test");
        Assert.Contains(metrics, metric => metric.Name == ServerTimingMiddleware.TotalMetricName);
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
        Key = "proxy-test",
        Slug = "proxy-test",
        IntegrationType = ToolIntegrationType.ProxiedApplication,
        UpstreamBaseUrl = upstream,
        Enabled = true
    };

    private static IReadOnlyList<TimingMetric> ReadMetricEntries(HttpContext context)
    {
        var metrics = new List<TimingMetric>();
        foreach (var rawMetric in context.Response.Headers[ServerTimingMiddleware.HeaderName]
                     .SelectMany(value => value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                         ?? []))
        {
            var segments = rawMetric.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.NotEmpty(segments);

            string? description = null;
            double? duration = null;
            foreach (var segment in segments.Skip(1))
            {
                if (segment.StartsWith("desc=", StringComparison.Ordinal))
                {
                    description = segment[5..].Trim('"');
                }
                else if (segment.StartsWith("dur=", StringComparison.Ordinal))
                {
                    Assert.True(double.TryParse(
                        segment[4..],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var parsedDuration));
                    duration = parsedDuration;
                }
            }

            Assert.NotNull(duration);
            metrics.Add(new TimingMetric(segments[0], description, duration.Value));
        }

        return metrics;
    }

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

    private sealed record TimingMetric(
        string Name,
        string? Description,
        double DurationMilliseconds);
}
