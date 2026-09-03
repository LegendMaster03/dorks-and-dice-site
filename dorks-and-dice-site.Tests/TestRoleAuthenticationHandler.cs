using System.Security.Claims;
using System.Text.Encodings.Web;
using dorks_and_dice_site.Models.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace dorks_and_dice_site.Tests;

public sealed class TestRoleAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public new const string Scheme = "IntegrationTestRoles";
    public const string RolesHeader = "X-Test-Roles";
    public const string ScopedRolesHeader = "X-Test-Scoped-Roles";

    public TestRoleAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var hasRoles = Request.Headers.TryGetValue(RolesHeader, out var rawRoles);
        var hasScopedRoles = Request.Headers.TryGetValue(ScopedRolesHeader, out var rawScopedRoles);
        if (!hasRoles && !hasScopedRoles)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "integration-test-user"),
            new(ClaimTypes.Name, "Integration Test User")
        };
        if (hasRoles)
        {
            claims.AddRange(rawRoles
                .SelectMany(value => value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [])
                .Distinct(StringComparer.Ordinal)
                .Select(role => new Claim(ClaimTypes.Role, role)));
        }
        if (hasScopedRoles)
        {
            claims.AddRange(rawScopedRoles
                .SelectMany(value => value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [])
                .Distinct(StringComparer.Ordinal)
                .Select(role => new Claim(AccountClaimTypes.ScopedRole, role)));
        }

        var identity = new ClaimsIdentity(claims, Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
