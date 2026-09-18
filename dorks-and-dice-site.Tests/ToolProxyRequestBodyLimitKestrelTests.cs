using System.Net;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace dorks_and_dice_site.Tests;

public sealed class ToolProxyRequestBodyLimitKestrelTests
{
    private const long OldKestrelLimitBytes = 30_000_000;
    private const long AboveOldKestrelLimitBytes = 32L * 1024 * 1024;

    [Fact]
    public async Task ToolHostUpstreamCanExceedOldKestrelLimitWhileOrdinaryRouteRetainsIt()
    {
        await using var app = await StartServerAsync(128L * 1024 * 1024);
        using var client = Client(app);

        using var upstreamRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/tool-host/test-tool/api/upstream/maps/import")
        {
            Content = new GeneratedContent(AboveOldKestrelLimitBytes, reportLength: true)
        };
        using var upstreamResponse = await client.SendAsync(upstreamRequest);
        Assert.Equal(HttpStatusCode.OK, upstreamResponse.StatusCode);
        Assert.Equal(
            AboveOldKestrelLimitBytes.ToString(),
            await upstreamResponse.Content.ReadAsStringAsync());

        using var ordinaryLimitResponse = await client.GetAsync("/ordinary-limit");
        Assert.Equal(HttpStatusCode.OK, ordinaryLimitResponse.StatusCode);
        Assert.Equal(
            OldKestrelLimitBytes.ToString(),
            await ordinaryLimitResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ChunkedToolHostUpstreamBodyOverConfiguredLimitReturnsPayloadTooLarge()
    {
        const long configuredLimit = 1024 * 1024;
        await using var app = await StartServerAsync(configuredLimit);
        using var client = Client(app);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/tool-host/test-tool/api/upstream/maps/import")
        {
            Content = new GeneratedContent(configuredLimit + 1, reportLength: false)
        };
        request.Headers.TransferEncodingChunked = true;

        using var response = await client.SendAsync(request);

        Assert.Equal((HttpStatusCode)StatusCodes.Status413PayloadTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task ChunkedDelegatedUpstreamBodyOverConfiguredLimitReturnsPayloadTooLarge()
    {
        const long configuredLimit = 1024 * 1024;
        await using var app = await StartServerAsync(configuredLimit);
        using var client = Client(app);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/tool-host/source/api/delegate/target/upstream/maps/import")
        {
            Content = new GeneratedContent(configuredLimit + 1, reportLength: false)
        };
        request.Headers.TransferEncodingChunked = true;

        using var response = await client.SendAsync(request);

        Assert.Equal((HttpStatusCode)StatusCodes.Status413PayloadTooLarge, response.StatusCode);
    }

    [Theory]
    [InlineData("/tool-host/tool/api/upstream")]
    [InlineData("/tool-host/tool/api/upstream/")]
    [InlineData("/tool-host/tool/api/upstream/maps/import")]
    [InlineData("/TOOL-HOST/tool/API/UPSTREAM/maps/import")]
    [InlineData("/tool-host/source/api/delegate/target/upstream")]
    [InlineData("/tool-host/source/api/delegate/target/upstream/")]
    [InlineData("/tool-host/source/api/delegate/target/upstream/maps/import")]
    [InlineData("/TOOL-HOST/source/API/DELEGATE/target/UPSTREAM/maps/import")]
    public void RouteMatcherIncludesOnlyToolHostUpstreamNamespace(string path)
    {
        Assert.True(ToolProxyRequestBodyLimitMiddleware.IsToolHostUpstreamRequest(new PathString(path)));
    }

    [Theory]
    [InlineData("/tool-host/tool/api")]
    [InlineData("/tool-host/tool/api/session")]
    [InlineData("/tool-host/tool/api/introspect")]
    [InlineData("/tool-host/tool/context")]
    [InlineData("/tools/tool/api/upstream")]
    [InlineData("/tool-host/tool/api/upstream-other")]
    [InlineData("/tool-host/source/api/delegate")]
    [InlineData("/tool-host/source/api/delegate/target")]
    [InlineData("/tool-host/source/api/delegate/target/not-upstream")]
    public void RouteMatcherExcludesNonUpstreamSiteRoutes(string path)
    {
        Assert.False(ToolProxyRequestBodyLimitMiddleware.IsToolHostUpstreamRequest(new PathString(path)));
    }

    private static async Task<WebApplication> StartServerAsync(long maxRequestBodyBytes)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, 0);
            options.Limits.MaxRequestBodySize = OldKestrelLimitBytes;
        });
        builder.Services.AddSingleton<IOptions<ToolProxyOptions>>(Options.Create(new ToolProxyOptions
        {
            MaxRequestBodyBytes = maxRequestBodyBytes,
            RequestTimeout = TimeSpan.FromMinutes(5)
        }));

        var app = builder.Build();
        app.UseRouting();
        app.UseMiddleware<ToolProxyRequestBodyLimitMiddleware>();
        app.MapPost(
            "/tool-host/{slug}/api/upstream/{**proxyPath}",
            (HttpContext context) => CountBodyAsync(context));
        app.MapPost(
            "/tool-host/{slug}/api/delegate/{targetSlug}/upstream/{**proxyPath}",
            (HttpContext context) => CountBodyAsync(context));
        app.MapGet("/ordinary-limit", async context =>
        {
            var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            await context.Response.WriteAsync(feature?.MaxRequestBodySize?.ToString() ?? "null");
        });
        await app.StartAsync();
        return app;
    }

    private static async Task CountBodyAsync(HttpContext context)
    {
        var buffer = new byte[64 * 1024];
        long total = 0;
        while (true)
        {
            var read = await context.Request.Body.ReadAsync(buffer, context.RequestAborted);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        await context.Response.WriteAsync(total.ToString(), context.RequestAborted);
    }

    private static HttpClient Client(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();
        var address = Assert.Single(addresses!.Addresses);
        return new HttpClient
        {
            BaseAddress = new Uri(address),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    private sealed class GeneratedContent(long length, bool reportLength) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            WriteAsync(stream, CancellationToken.None);

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken) =>
            WriteAsync(stream, cancellationToken);

        protected override bool TryComputeLength(out long computedLength)
        {
            computedLength = length;
            return reportLength;
        }

        private async Task WriteAsync(Stream stream, CancellationToken cancellationToken)
        {
            var buffer = new byte[64 * 1024];
            Array.Fill(buffer, (byte)0x31);
            var remaining = length;
            while (remaining > 0)
            {
                var count = (int)Math.Min(buffer.Length, remaining);
                await stream.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                remaining -= count;
            }
        }
    }
}
