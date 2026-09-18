using System.Security.Claims;
using System.Text.Encodings.Web;
using dorks_and_dice_site.Framework.Operator;
using dorks_and_dice_site.Models.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace dorks_and_dice_site.Services.Operator;

public sealed class OperatorAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IOperatorCredentialService _credentialService;
    private readonly IUserClaimsPrincipalFactory<ApplicationUser> _principalFactory;

    public OperatorAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOperatorCredentialService credentialService,
        IUserClaimsPrincipalFactory<ApplicationUser> principalFactory)
        : base(options, logger, encoder)
    {
        _credentialService = credentialService;
        _principalFactory = principalFactory;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";
        if (!authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authorization[bearerPrefix.Length..].Trim();
        var authenticated = await _credentialService.AuthenticateAsync(token, Context.RequestAborted);
        if (authenticated is null)
        {
            return AuthenticateResult.Fail("Invalid operator credential.");
        }

        var principal = await _principalFactory.CreateAsync(authenticated.User);
        if (principal.Identity is not ClaimsIdentity identity)
        {
            return AuthenticateResult.Fail("The service principal did not produce a claims identity.");
        }

        identity.AddClaim(new Claim(
            OperatorClaimTypes.CredentialId,
            authenticated.Credential.Id.ToString("D")));
        identity.AddClaim(new Claim(
            OperatorClaimTypes.Client,
            authenticated.Credential.Name));

        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
