using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class ToolLifecycleIntrospectionController : ControllerBase
{
    [HttpPost("/tool-host/{slug}/api/lifecycle/introspect")]
    public IActionResult Introspect(string slug)
    {
        Response.Headers.CacheControl = "no-store";

        var authorization = Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";
        if (!authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized();
        }

        var ticket = authorization[bearerPrefix.Length..].Trim();
        if (!ToolLifecycleTickets.TryRedeem(slug, ticket, out var context)
            || context is null)
        {
            return Unauthorized();
        }

        return Ok(context);
    }
}
