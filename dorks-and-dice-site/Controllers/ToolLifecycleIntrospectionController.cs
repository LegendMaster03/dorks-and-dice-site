using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class ToolLifecycleIntrospectionController(IToolRegistry toolRegistry) : ControllerBase
{
    [HttpPost("/tool-host/{slug}/api/lifecycle/introspect")]
    public async Task<IActionResult> IntrospectBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        var registration = await toolRegistry.GetBySlugAsync(slug, cancellationToken);
        return registration is null
            ? Unauthorized()
            : IntrospectRegistration(registration.Key);
    }

    [HttpPost("/tool-host/registrations/{registrationKey}/api/lifecycle/introspect")]
    public async Task<IActionResult> IntrospectByKey(
        string registrationKey,
        CancellationToken cancellationToken)
    {
        var registration = await toolRegistry.GetByKeyAsync(registrationKey, cancellationToken);
        return registration is null
            ? Unauthorized()
            : IntrospectRegistration(registration.Key);
    }

    private IActionResult IntrospectRegistration(string registrationKey)
    {
        Response.Headers.CacheControl = "no-store";

        var authorization = Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";
        if (!authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized();
        }

        var ticket = authorization[bearerPrefix.Length..].Trim();
        if (!ToolLifecycleTickets.TryRedeem(registrationKey, ticket, out var context)
            || context is null)
        {
            return Unauthorized();
        }

        return Ok(context);
    }
}
