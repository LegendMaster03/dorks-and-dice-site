using System.Security.Claims;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Authorize]
[Route("tool-host/{slug}/api")]
public sealed class ToolHostApiController : ControllerBase
{
    private readonly IToolRegistry _toolRegistry;
    private readonly ICampaignContextService _campaignContextService;
    private readonly IToolHostAuthenticationContextFactory _authenticationContextFactory;
    private readonly IToolProxyService _toolProxyService;
    private readonly IToolDelegationCapabilityService _delegationCapabilities;
    private readonly IToolUpstreamPolicy _upstreamPolicy;
    private readonly IConfiguration _configuration;

    public ToolHostApiController(
        IToolRegistry toolRegistry,
        ICampaignContextService campaignContextService,
        IToolHostAuthenticationContextFactory authenticationContextFactory,
        IToolProxyService toolProxyService,
        IToolDelegationCapabilityService delegationCapabilities,
        IToolUpstreamPolicy upstreamPolicy,
        IConfiguration configuration)
    {
        _toolRegistry = toolRegistry;
        _campaignContextService = campaignContextService;
        _authenticationContextFactory = authenticationContextFactory;
        _toolProxyService = toolProxyService;
        _delegationCapabilities = delegationCapabilities;
        _upstreamPolicy = upstreamPolicy;
        _configuration = configuration;
    }

    [HttpGet("session")]
    public async Task<IActionResult> Session(string slug, CancellationToken cancellationToken)
    {
        var access = await ResolveToolAndUserAsync(slug, cancellationToken);
        if (access.Result is not null)
        {
            return access.Result;
        }

        Response.Headers.CacheControl = "no-store";

        return Ok(new ToolHostApiSession
        {
            ToolKey = access.Tool!.Key,
            ToolSlug = access.Tool.Slug!,
            SiteMode = HttpContext.GetSiteModeContext().ActiveModeId!,
            User = BuildUserContext(access.UserId!),
            GlobalRoles = BuildEffectiveGlobalRoles()
        });
    }

    [HttpGet("campaigns")]
    public async Task<IActionResult> Campaigns(string slug, CancellationToken cancellationToken)
    {
        var access = await ResolveToolAndUserAsync(slug, cancellationToken);
        if (access.Result is not null)
        {
            return access.Result;
        }

        if (!Guid.TryParse(access.UserId, out var userId))
        {
            return Forbid();
        }

        var campaigns = await _campaignContextService.GetAccessibleCampaignsAsync(
            userId,
            cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(campaigns.Select(ToToolHostSummary).ToArray());
    }

    [HttpGet("campaigns/{campaignId:guid}")]
    public async Task<IActionResult> Campaign(
        string slug,
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var access = await ResolveToolAndUserAsync(slug, cancellationToken);
        if (access.Result is not null)
        {
            return access.Result;
        }

        if (!Guid.TryParse(access.UserId, out var userId))
        {
            return Forbid();
        }

        var campaign = await _campaignContextService.GetCampaignContextAsync(
            userId,
            campaignId,
            cancellationToken);
        if (campaign is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store";
        return Ok(ToToolHostSummary(campaign));
    }

    /// <summary>
    /// Returns the stable native campaign projection used by first-party Tools that need roster
    /// data. Membership is derived exclusively from the authenticated site identity; callers can
    /// not supply another user ID. The projection intentionally contains participants and linked
    /// characters without exposing Dorks & Dice persistence entities.
    /// </summary>
    [HttpGet("campaigns/{campaignId:guid}/context")]
    public async Task<IActionResult> CampaignContext(
        string slug,
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var access = await ResolveToolAndUserAsync(slug, cancellationToken);
        if (access.Result is not null)
        {
            return access.Result;
        }

        if (!Guid.TryParse(access.UserId, out var userId))
        {
            return Forbid();
        }

        var campaign = await _campaignContextService.GetCampaignContextAsync(
            userId,
            campaignId,
            cancellationToken);
        if (campaign is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store";
        return Ok(campaign);
    }

    /// <summary>
    /// Gateway for an Embedded Module to call its Tool backend. Anonymous requests are proxied only
    /// when the Tool is explicitly registered with AllowAnonymous=true, and receive no trusted
    /// identity headers. Authenticated requests retain the ticket/introspection contract.
    /// </summary>
    [AllowAnonymous]
    [DisableFormValueModelBinding]
    [AcceptVerbs("GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")]
    [Route("upstream")]
    [Route("upstream/{**proxyPath}")]
    public async Task<IActionResult> Upstream(
        [FromRoute] string slug,
        [FromRoute] string? proxyPath,
        CancellationToken cancellationToken)
    {
        var tool = await ResolveAvailableToolAsync(slug, cancellationToken);
        if (tool is null || tool.IntegrationType != ToolIntegrationType.EmbeddedModule)
        {
            return NotFound();
        }

        if (ToolIntegrationContractPolicy.GetUnsupportedReason(tool) is { } contractError)
        {
            return Problem(
                title: "Unsupported tool integration contract",
                detail: contractError,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (string.IsNullOrWhiteSpace(tool.UpstreamBaseUrl))
        {
            return NotFound();
        }

        var upstreamPath = string.IsNullOrWhiteSpace(proxyPath) ? "/" : $"/{proxyPath}";
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            if (User.Identity?.IsAuthenticated == true || !tool.AllowAnonymous)
            {
                return Challenge();
            }

            await _toolProxyService.ProxyAsync(
                HttpContext,
                tool,
                upstreamPath,
                cancellationToken);
            return new EmptyResult();
        }

        var authenticationContext = await _authenticationContextFactory.CreateAsync(
            tool,
            User,
            HttpContext.GetSiteModeContext().ActiveModeId!,
            cancellationToken);
        if (authenticationContext is null)
        {
            return Forbid();
        }

        var ticket = ToolAuthenticationTickets.Issue(authenticationContext);
        var introspectionPath = AuthenticationIntrospectionPath(tool);

        await _toolProxyService.ProxyAuthenticatedAsync(
            HttpContext,
            tool,
            upstreamPath,
            ticket,
            introspectionPath,
            cancellationToken);
        return new EmptyResult();
    }

    /// <summary>
    /// Redeems a one-time authentication ticket presented by an application backend. This legacy
    /// slug route is retained for existing application integrations. Tickets issued before stable
    /// registration keys were added remain redeemable because their authoritative scope was the
    /// application slug.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("introspect")]
    public async Task<IActionResult> Introspect(
        string slug,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var registration = await _toolRegistry.GetBySlugAsync(slug, cancellationToken);
        return IntrospectRegistration(registration?.Key ?? slug, registration);
    }

    /// <summary>
    /// Stable-key introspection endpoint used by headless services. It deliberately has no public
    /// /tools route counterpart.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("~/tool-host/registrations/{registrationKey}/api/introspect")]
    public async Task<IActionResult> IntrospectRegistrationByKey(
        string registrationKey,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var registration = await _toolRegistry.GetByKeyAsync(registrationKey, cancellationToken);
        return registration is null
            ? Unauthorized()
            : IntrospectRegistration(registration.Key, registration);
    }

    [AllowAnonymous]
    [DisableFormValueModelBinding]
    [AcceptVerbs("GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")]
    [Route("delegate/{targetKey}/upstream")]
    [Route("delegate/{targetKey}/upstream/{**proxyPath}")]
    public async Task<IActionResult> DelegatedUpstream(
        [FromRoute] string slug,
        [FromRoute] string targetKey,
        [FromRoute] string? proxyPath,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var sourceTool = await _toolRegistry.GetBySlugAsync(slug, cancellationToken);
        if (sourceTool is null || sourceTool.Kind != ToolKind.Application)
        {
            return Unauthorized();
        }

        return await DelegatedUpstreamCoreAsync(
            sourceTool,
            targetKey,
            proxyPath,
            cancellationToken);
    }

    [AllowAnonymous]
    [DisableFormValueModelBinding]
    [AcceptVerbs("GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")]
    [Route("~/tool-host/registrations/{sourceKey}/api/delegate/{targetKey}/upstream")]
    [Route("~/tool-host/registrations/{sourceKey}/api/delegate/{targetKey}/upstream/{**proxyPath}")]
    public async Task<IActionResult> DelegatedUpstreamByKey(
        [FromRoute] string sourceKey,
        [FromRoute] string targetKey,
        [FromRoute] string? proxyPath,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var sourceTool = await _toolRegistry.GetByKeyAsync(sourceKey, cancellationToken);
        if (sourceTool is null)
        {
            return Unauthorized();
        }

        return await DelegatedUpstreamCoreAsync(
            sourceTool,
            targetKey,
            proxyPath,
            cancellationToken);
    }

    private IActionResult IntrospectRegistration(
        string registrationKey,
        ToolRegistration? registration)
    {
        Response.Headers.CacheControl = "no-store";

        if (!TryReadBearerToken(out var ticket)
            || !ToolAuthenticationTickets.TryRedeem(
                registrationKey,
                ticket,
                out var context)
            || context is null)
        {
            return Unauthorized();
        }

        string? sourceCapability = null;
        if (registration is not null && CanIssueDelegationCapability(registration, context))
        {
            sourceCapability = _delegationCapabilities.Issue(registration.Key, context);
            Response.Headers[ToolDelegationHeaders.Capability] = sourceCapability;
            Response.Headers[ToolDelegationHeaders.Path] = DelegationPathTemplate(registration);
        }

        if (registration is not null && CanIssuePrivateTunnelCapability(registration, context))
        {
            sourceCapability ??= _delegationCapabilities.Issue(registration.Key, context);
            Response.Headers[ToolPrivateTunnelHeaders.Capability] = sourceCapability;
        }

        return Ok(context);
    }

    private async Task<IActionResult> DelegatedUpstreamCoreAsync(
        ToolRegistration sourceTool,
        string targetKey,
        string? proxyPath,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";

        if (!TryReadBearerToken(out var capability)
            || !_delegationCapabilities.TryUse(
                sourceTool.Key,
                capability,
                out var sourceContext)
            || sourceContext is null)
        {
            return Unauthorized();
        }

        if (!IsDelegationSourceAvailable(sourceTool, sourceContext.SiteMode))
        {
            return Unauthorized();
        }

        if (!sourceTool.DelegationTargets.Contains(
                targetKey,
                StringComparer.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var targetTool = await _toolRegistry.GetByKeyAsync(targetKey, cancellationToken);
        if (targetTool is null
            || !targetTool.Enabled
            || !ToolVisibility.IsVisibleInMode(targetTool, sourceContext.SiteMode))
        {
            return NotFound();
        }

        if (!IsDelegationTargetSupported(targetTool, out var contractError))
        {
            return Problem(
                title: "Unsupported tool integration contract",
                detail: contractError,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var upstreamPath = string.IsNullOrWhiteSpace(proxyPath) ? "/" : $"/{proxyPath}";
        if (!_upstreamPolicy.TryBuild(
                targetTool,
                upstreamPath,
                Request.QueryString,
                out _,
                out _))
        {
            return StatusCode(StatusCodes.Status502BadGateway);
        }

        var targetContext = await _authenticationContextFactory.CreateDelegatedAsync(
            targetTool,
            sourceContext,
            cancellationToken);
        if (targetContext is null)
        {
            return Unauthorized();
        }

        var targetTicket = ToolAuthenticationTickets.Issue(targetContext);
        var introspectionPath = AuthenticationIntrospectionPath(targetTool);

        await _toolProxyService.ProxyAuthenticatedAsync(
            HttpContext,
            targetTool,
            upstreamPath,
            targetTicket,
            introspectionPath,
            cancellationToken);
        return new EmptyResult();
    }

    private static bool CanIssueDelegationCapability(
        ToolRegistration sourceTool,
        ToolHostAuthenticationContext context) =>
        sourceTool.Enabled
        && sourceTool.DelegationTargets.Count > 0
        && ToolVisibility.IsVisibleInMode(sourceTool, context.SiteMode)
        && IsDelegationSourceSupported(sourceTool);

    private bool CanIssuePrivateTunnelCapability(
        ToolRegistration sourceTool,
        ToolHostAuthenticationContext context) =>
        sourceTool.Enabled
        && ToolPrivateTunnelPolicy.HasTargets(_configuration, sourceTool.Key)
        && ToolVisibility.IsVisibleInMode(sourceTool, context.SiteMode)
        && ToolPrivateTunnelPolicy.IsSupportedSource(sourceTool);

    private static bool IsDelegationSourceAvailable(ToolRegistration sourceTool, string siteMode) =>
        sourceTool.Enabled
        && ToolVisibility.IsVisibleInMode(sourceTool, siteMode)
        && IsDelegationSourceSupported(sourceTool);

    private static bool IsDelegationSourceSupported(ToolRegistration sourceTool) =>
        sourceTool.Kind == ToolKind.Service
        || (sourceTool.Kind == ToolKind.Application
            && sourceTool.IntegrationType == ToolIntegrationType.EmbeddedModule
            && ToolIntegrationContractPolicy.IsSupported(sourceTool));

    private static bool IsDelegationTargetSupported(
        ToolRegistration targetTool,
        out string? error)
    {
        error = null;
        if (targetTool.Kind == ToolKind.Service)
        {
            return true;
        }

        if (targetTool.Kind != ToolKind.Application
            || targetTool.IntegrationType != ToolIntegrationType.EmbeddedModule)
        {
            error = "Delegated application targets must use the Embedded Module integration contract.";
            return false;
        }

        error = ToolIntegrationContractPolicy.GetUnsupportedReason(targetTool);
        return error is null;
    }

    private static string AuthenticationIntrospectionPath(ToolRegistration tool) =>
        tool.Kind == ToolKind.Application && !string.IsNullOrWhiteSpace(tool.Slug)
            ? $"/tool-host/{tool.Slug}/api/introspect"
            : $"/tool-host/registrations/{tool.Key}/api/introspect";

    private static string DelegationPathTemplate(ToolRegistration sourceTool) =>
        sourceTool.Kind == ToolKind.Application && !string.IsNullOrWhiteSpace(sourceTool.Slug)
            ? $"/tool-host/{sourceTool.Slug}/api/delegate/{{targetSlug}}/upstream"
            : $"/tool-host/registrations/{sourceTool.Key}/api/delegate/{{targetKey}}/upstream";

    private bool TryReadBearerToken(out string token)
    {
        token = string.Empty;
        var authorization = Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";
        if (!authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token = authorization[bearerPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(token);
    }

    private static ToolHostCampaignAccessSummary ToToolHostSummary(CampaignAccessContext campaign) => new()
    {
        Id = campaign.CampaignId,
        Name = campaign.Name,
        Role = PreferredToolHostRole(campaign.Roles)
    };

    private static ToolHostCampaignAccessSummary ToToolHostSummary(CampaignContextSnapshot campaign) => new()
    {
        Id = campaign.CampaignId,
        Name = campaign.Name,
        Role = PreferredToolHostRole(campaign.RequestingUserRoles)
    };

    private static string PreferredToolHostRole(IReadOnlyList<string> roles) =>
        roles.Any(role => string.Equals(role, CampaignRoles.Dm, StringComparison.OrdinalIgnoreCase))
            ? "DM"
            : "Player";

    private async Task<(ToolRegistration? Tool, string? UserId, IActionResult? Result)>
        ResolveToolAndUserAsync(string slug, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var tool = await ResolveAvailableToolAsync(slug, cancellationToken);
        if (tool is null)
        {
            return (null, null, NotFound());
        }

        if (ToolIntegrationContractPolicy.GetUnsupportedReason(tool) is { } contractError)
        {
            return (
                tool,
                null,
                Problem(
                    title: "Unsupported tool integration contract",
                    detail: contractError,
                    statusCode: StatusCodes.Status503ServiceUnavailable));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return (tool, null, Challenge());
        }

        return (tool, userId, null);
    }

    private async Task<ToolRegistration?> ResolveAvailableToolAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        var tool = await _toolRegistry.GetBySlugAsync(slug, cancellationToken);
        var modeId = HttpContext.GetSiteModeContext().ActiveModeId;
        return tool is not null
            && tool.Kind == ToolKind.Application
            && !string.IsNullOrWhiteSpace(tool.Slug)
            && tool.Enabled
            && ToolVisibility.IsVisibleInMode(tool, modeId)
            ? tool
            : null;
    }

    private IReadOnlyList<string> BuildEffectiveGlobalRoles() =>
        AccountRoleHierarchy.GlobalRoleNames
            .Where(role => AccountRoleHierarchy.PrincipalHasGlobalRole(User, role))
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();

    private ToolHostUserContext BuildUserContext(string userId) => new()
    {
        Id = userId,
        DisplayName = User.FindFirstValue(AccountClaimTypes.DisplayName)
            ?? User.Identity?.Name
            ?? string.Empty
    };
}
