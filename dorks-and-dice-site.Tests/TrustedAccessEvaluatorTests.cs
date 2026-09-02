using System.Net;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Http;

namespace dorks_and_dice_site.Tests;

public sealed class TrustedAccessEvaluatorTests
{
    private readonly SiteModeOptions _options = new();

    [Fact]
    public void GenuineLocalhostRequestIsAuthorized()
    {
        var context = CreateContext("localhost", IPAddress.Loopback, localPort: 5000);
        TrustedAccessEvaluator.CaptureOriginalConnection(context);

        Assert.True(TrustedAccessEvaluator.IsAuthorized(context, _options));
    }

    [Fact]
    public void ForwardedLoopbackCanNotReplaceTheOriginalPrivateNetworkPeer()
    {
        var context = CreateContext("public.example", IPAddress.Parse("10.0.0.25"), localPort: 8080);
        TrustedAccessEvaluator.CaptureOriginalConnection(context);

        context.Request.Host = new HostString("localhost");
        context.Connection.RemoteIpAddress = IPAddress.Loopback;

        Assert.False(TrustedAccessEvaluator.IsAuthorized(context, _options));
    }

    [Fact]
    public void SpoofedLocalhostHostFromNonLoopbackAddressIsRejected()
    {
        var context = CreateContext("localhost", IPAddress.Parse("10.0.0.25"), localPort: 5000);

        Assert.False(TrustedAccessEvaluator.IsAuthorized(context, _options));
    }

    [Fact]
    public void DirectServerIpDoesNotGrantTrustedAccess()
    {
        var context = CreateContext("10.0.0.7", IPAddress.Parse("10.0.0.25"), localPort: 8080);

        Assert.False(TrustedAccessEvaluator.IsAuthorized(context, _options));
    }

    [Fact]
    public void ForgedCapabilityOnNormalIngressIsRejected()
    {
        var context = CreateContext("10.0.0.7", IPAddress.Parse("10.0.0.25"), localPort: 8080);
        context.Request.Headers[TrustedAccessEvaluator.AppCapabilitiesHeader] =
            "{\"dorks-and-dice.com/cap/dev-mode\":[{}]}";

        Assert.False(TrustedAccessEvaluator.IsAuthorized(context, _options));
    }

    [Fact]
    public void TrustedTailscaleIngressWithDevelopmentCapabilityIsAuthorized()
    {
        var context = CreateContext(
            "dorks-and-dice.example.ts.net",
            IPAddress.Parse("172.16.0.1"),
            TrustedAccessEvaluator.TrustedTailscaleIngressPort);
        context.Request.Headers[TrustedAccessEvaluator.AppCapabilitiesHeader] =
            "{\"dorks-and-dice.com/cap/dev-mode\":[{}]}";

        Assert.True(TrustedAccessEvaluator.IsAuthorized(context, _options));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"dorks-and-dice.com/cap/dev-mode\":[]}")]
    [InlineData("{\"other.example/cap/dev-mode\":[{}]}")]
    public void TrustedTailscaleIngressWithoutValidCapabilityIsRejected(string capabilityHeader)
    {
        var context = CreateContext(
            "dorks-and-dice.example.ts.net",
            IPAddress.Parse("172.16.0.1"),
            TrustedAccessEvaluator.TrustedTailscaleIngressPort);
        if (capabilityHeader.Length > 0)
        {
            context.Request.Headers[TrustedAccessEvaluator.AppCapabilitiesHeader] = capabilityHeader;
        }

        Assert.False(TrustedAccessEvaluator.IsAuthorized(context, _options));
    }

    private static DefaultHttpContext CreateContext(string host, IPAddress remoteAddress, int localPort)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        context.Connection.RemoteIpAddress = remoteAddress;
        context.Connection.LocalPort = localPort;
        return context;
    }
}
