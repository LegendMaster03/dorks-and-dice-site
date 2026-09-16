using System.Net;
using System.Net.Http.Headers;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class ToolProxyTransportIntegrationTests(PublishedContentWebApplicationFactory factory)
{
    private const long LargeMultipartPayloadBytes = 34L * 1024 * 1024;

    [Fact]
    public void ProxyTransportDefaultsAndNamedClientTimeoutComeFromOptions()
    {
        var options = factory.Services.GetRequiredService<IOptions<ToolProxyOptions>>().Value;
        Assert.Equal(128L * 1024 * 1024, options.MaxRequestBodyBytes);
        Assert.Equal(TimeSpan.FromMinutes(5), options.RequestTimeout);

        using var client = factory.Services
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient(ToolHttpClientNames.Proxy);
        Assert.Equal(TimeSpan.FromMinutes(5), client.Timeout);
    }

    [Fact]
    public void ProxyTransportConfigurationOverridesDefaultsAndNamedClientTimeout()
    {
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ToolHosting:Proxy:MaxRequestBodyBytes", (64L * 1024 * 1024).ToString());
            builder.UseSetting("ToolHosting:Proxy:RequestTimeout", "00:00:07");
        });

        var options = host.Services.GetRequiredService<IOptions<ToolProxyOptions>>().Value;
        Assert.Equal(64L * 1024 * 1024, options.MaxRequestBodyBytes);
        Assert.Equal(TimeSpan.FromSeconds(7), options.RequestTimeout);

        using var client = host.Services
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient(ToolHttpClientNames.Proxy);
        Assert.Equal(TimeSpan.FromSeconds(7), client.Timeout);
    }

    [Theory]
    [InlineData("ToolHosting:Proxy:MaxRequestBodyBytes", "0")]
    [InlineData("ToolHosting:Proxy:MaxRequestBodyBytes", "-1")]
    [InlineData("ToolHosting:Proxy:RequestTimeout", "00:00:00")]
    [InlineData("ToolHosting:Proxy:RequestTimeout", "-00:00:01")]
    public void InvalidProxyTransportConfigurationFailsStartup(string key, string value)
    {
        using var host = factory.WithWebHostBuilder(builder => builder.UseSetting(key, value));

        var exception = Assert.ThrowsAny<Exception>(() => host.CreateClient());
        Assert.Contains("must be greater than zero", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthenticatedEmbeddedMultipartUploadAboveOldKestrelLimitStreamsCompletePayload()
    {
        var capture = new MultipartCapture();
        await using var upstream = await StartMultipartCaptureUpstreamAsync(capture);
        var upstreamBaseUrl = GetServerAddress(upstream);
        using var host = factory.WithWebHostBuilder(_ => { });
        host.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        host.StartServer();
        var tool = await RegisterAsync(host, upstreamBaseUrl);

        try
        {
            using var client = host.CreateClient();
            const string boundary = "dorks-large-upload-boundary";
            using var multipart = new MultipartFormDataContent(boundary);
            using var fileContent = new GeneratedContent(LargeMultipartPayloadBytes, reportLength: true);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            multipart.Add(fileContent, "map", "map.bin");
            var expectedBodyBytes = Assert.IsType<long>(multipart.Headers.ContentLength);
            Assert.True(expectedBodyBytes > LargeMultipartPayloadBytes);
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"/tool-host/{tool.Slug}/api/upstream/maps/import")
            {
                Content = multipart
            };
            request.Headers.Host = "dorks-and-dice.com";
            request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, "Member");
            request.Headers.Add(ToolAuthenticationHeaders.Ticket, "browser-spoof");

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal(HttpMethod.Post, capture.Method);
            Assert.Equal("multipart/form-data", capture.MediaType);
            Assert.Equal(boundary, capture.Boundary);
            Assert.Equal(expectedBodyBytes, capture.ContentLength);
            Assert.Equal(expectedBodyBytes, capture.BodyBytes);
            Assert.True(capture.TrustedTicketWasInjected);
        }
        finally
        {
            await DeleteAsync(host, tool.Id);
        }
    }

    [Fact]
    public async Task EmbeddedUpstreamPreservesBinaryResponseStatusHeadersAndBytes()
    {
        var bytes = Enumerable.Range(0, 1024)
            .Select(index => (byte)((index * 37) % 256))
            .ToArray();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddHttpClient(ToolHttpClientNames.Proxy)
                .ConfigurePrimaryHttpMessageHandler(() => new AsyncHandler((request, _) =>
                {
                    Assert.Equal(HttpMethod.Get, request.Method);
                    var content = new ByteArrayContent(bytes);
                    content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                    content.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
                    {
                        FileName = "map.png"
                    };
                    content.Headers.ContentRange = new ContentRangeHeaderValue(0, bytes.Length - 1, bytes.Length);
                    var upstream = new HttpResponseMessage(HttpStatusCode.PartialContent)
                    {
                        Content = content
                    };
                    upstream.Headers.ETag = new EntityTagHeaderValue("\"map-v1\"");
                    upstream.Headers.TryAddWithoutValidation("Set-Cookie", "blocked=1");
                    return Task.FromResult(upstream);
                }))));
        var tool = await RegisterAsync(host);

        try
        {
            using var client = Client(host);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/tool-host/{tool.Slug}/api/upstream/maps/map.png");
            request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, "Member");

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
            Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("inline", response.Content.Headers.ContentDisposition?.DispositionType);
            Assert.Equal(new ContentRangeHeaderValue(0, bytes.Length - 1, bytes.Length), response.Content.Headers.ContentRange);
            Assert.Equal("\"map-v1\"", response.Headers.ETag?.Tag);
            Assert.False(response.Headers.Contains("Set-Cookie"));
            Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
        }
        finally
        {
            await DeleteAsync(host, tool.Id);
        }
    }

    [Fact]
    public async Task ConfiguredContentLengthOverLimitReturnsPayloadTooLargeWithoutCallingUpstream()
    {
        var upstreamCalled = false;
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ToolHosting:Proxy:MaxRequestBodyBytes", "1048576");
            builder.ConfigureServices(services =>
                services.AddHttpClient(ToolHttpClientNames.Proxy)
                    .ConfigurePrimaryHttpMessageHandler(() => new AsyncHandler((_, _) =>
                    {
                        upstreamCalled = true;
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                    })));
        });
        var tool = await RegisterAsync(host);

        try
        {
            using var client = Client(host);
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"/tool-host/{tool.Slug}/api/upstream/maps/import")
            {
                Content = new GeneratedContent(1048577, reportLength: true)
            };
            request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, "Member");

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
            Assert.False(upstreamCalled);
        }
        finally
        {
            await DeleteAsync(host, tool.Id);
        }
    }

    [Fact]
    public async Task ConfiguredProxyTimeoutMapsToGatewayTimeoutWithoutThirtySecondWait()
    {
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ToolHosting:Proxy:RequestTimeout", "00:00:00.2500000");
            builder.ConfigureServices(services =>
                services.AddHttpClient(ToolHttpClientNames.Proxy)
                    .ConfigurePrimaryHttpMessageHandler(() => new AsyncHandler(async (_, cancellationToken) =>
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                        return new HttpResponseMessage(HttpStatusCode.OK);
                    })));
        });
        var tool = await RegisterAsync(host);

        try
        {
            using var client = Client(host);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/tool-host/{tool.Slug}/api/upstream/slow");
            request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, "Member");

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        }
        finally
        {
            await DeleteAsync(host, tool.Id);
        }
    }

    [Fact]
    public async Task BrowserCancellationStillCancelsUpstreamOperation()
    {
        var handler = new AsyncHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        var service = new ToolProxyService(
            new FixedHttpClientFactory(httpClient),
            new ToolUpstreamPolicy(new ConfigurationBuilder().Build()));
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("dorks-and-dice.com");
        context.Response.Body = new MemoryStream();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ProxyAsync(
            context,
            new ToolRegistration
            {
                Slug = "cancel-test",
                IntegrationType = ToolIntegrationType.EmbeddedModule,
                UpstreamBaseUrl = "http://cancel-test:8080",
                Enabled = true
            },
            "/slow",
            cancellation.Token));
    }

    private static async Task<WebApplication> StartMultipartCaptureUpstreamAsync(MultipartCapture capture)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));

        var app = builder.Build();
        app.MapPost("/{**proxyPath}", capture.HandleAsync);
        await app.StartAsync();
        return app;
    }

    private static string GetServerAddress(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();
        return Assert.Single(addresses!.Addresses);
    }

    private static async Task<ToolRegistration> RegisterAsync(
        WebApplicationFactory<Program> host,
        string upstreamBaseUrl = "http://transport-test:8080")
    {
        var tool = new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Slug = $"transport-{Guid.NewGuid():N}",
            DisplayName = "Transport Test Tool",
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            UpstreamBaseUrl = upstreamBaseUrl,
            FrontendEntryPoint = "/app.js",
            Modes = [SiteModeValues.DorksAndDiceModeValue],
            AllowAnonymous = false,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        using var scope = host.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
        await registry.SaveAsync(tool);
        return tool;
    }

    private static async Task DeleteAsync(WebApplicationFactory<Program> host, Guid id)
    {
        using var scope = host.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
        await registry.DeleteAsync(id);
    }

    private static HttpClient Client(WebApplicationFactory<Program> host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://dorks-and-dice.com")
        });

    private sealed class MultipartCapture
    {
        public HttpMethod? Method { get; private set; }
        public string? MediaType { get; private set; }
        public string? Boundary { get; private set; }
        public long? ContentLength { get; private set; }
        public long BodyBytes { get; private set; }
        public bool TrustedTicketWasInjected { get; private set; }

        public async Task HandleAsync(HttpContext context)
        {
            Method = new HttpMethod(context.Request.Method);
            if (MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var contentType))
            {
                MediaType = contentType.MediaType;
                Boundary = contentType.Parameters
                    .FirstOrDefault(parameter => string.Equals(parameter.Name, "boundary", StringComparison.OrdinalIgnoreCase))
                    ?.Value
                    ?.Trim('"');
            }

            ContentLength = context.Request.ContentLength;
            TrustedTicketWasInjected = context.Request.Headers.TryGetValue(ToolAuthenticationHeaders.Ticket, out var ticketValues)
                && ticketValues.Count == 1
                && ticketValues[0] != "browser-spoof";

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

            BodyBytes = total;
            context.Response.StatusCode = StatusCodes.Status201Created;
            await context.Response.WriteAsync("accepted", context.RequestAborted);
        }
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

        private static byte[] Buffer()
        {
            var buffer = new byte[64 * 1024];
            Array.Fill(buffer, (byte)0x5A);
            return buffer;
        }

        private async Task WriteAsync(Stream stream, CancellationToken cancellationToken)
        {
            var buffer = Buffer();
            var remaining = length;
            while (remaining > 0)
            {
                var count = (int)Math.Min(buffer.Length, remaining);
                await stream.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                remaining -= count;
            }
        }
    }

    private sealed class AsyncHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => respond(request, cancellationToken);
    }

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
