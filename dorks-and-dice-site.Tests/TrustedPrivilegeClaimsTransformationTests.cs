using System.Net;
using System.Security.Claims;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Http;

namespace dorks_and_dice_site.Tests;

public sealed class TrustedPrivilegeClaimsTransformationTests
{
    [Fact]
    public async Task PublicRequestSuppressesTrustedRolesButPreservesSafeInheritedAuthority()
    {
        var context = CreateContext("dorks-and-dice.com", IPAddress.Parse("10.0.0.25"), localPort: 8080);
        TrustedAccessEvaluator.CaptureOriginalConnection(context);
        var transformation = CreateTransformation(context);

        var transformed = await transformation.TransformAsync(CreatePrincipal());

        Assert.True(transformed.Identity?.IsAuthenticated);
        Assert.False(transformed.IsInRole(AccountRoles.Admin));
        Assert.False(transformed.IsInRole(AccountRoles.Dev));
        Assert.True(transformed.IsInRole(AccountRoles.GlobalEditor));
        Assert.True(transformed.IsInRole("Member"));
    }

    [Fact]
    public async Task TrustedRequestPreservesAdminAndDevRoleClaims()
    {
        var context = CreateContext("localhost", IPAddress.Loopback, localPort: 5000);
        TrustedAccessEvaluator.CaptureOriginalConnection(context);
        var transformation = CreateTransformation(context);

        var transformed = await transformation.TransformAsync(CreatePrincipal());

        Assert.True(transformed.IsInRole(AccountRoles.Admin));
        Assert.True(transformed.IsInRole(AccountRoles.Dev));
        Assert.True(transformed.IsInRole("Member"));
    }

    private static TrustedPrivilegeClaimsTransformation CreateTransformation(HttpContext context)
    {
        var accessor = new HttpContextAccessor { HttpContext = context };
        return new TrustedPrivilegeClaimsTransformation(accessor, new SiteModeOptions());
    }

    private static ClaimsPrincipal CreatePrincipal()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-id"),
                new Claim(ClaimTypes.Name, "Test User"),
                new Claim(ClaimTypes.Role, AccountRoles.Admin),
                new Claim(ClaimTypes.Role, AccountRoles.Dev),
                new Claim(ClaimTypes.Role, "Member")
            ],
            authenticationType: "Test");
        return new ClaimsPrincipal(identity);
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
