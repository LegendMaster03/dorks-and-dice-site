using System.Security.Claims;
using dorks_and_dice_site.Framework.Operator;
using dorks_and_dice_site.Models.Operator;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace dorks_and_dice_site.Services.Operator;

public sealed class OperatorAuditFilter(IdentityDbContext dbContext) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var principal = context.HttpContext.User;
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(principal.FindFirstValue(OperatorClaimTypes.CredentialId), out var credentialId))
        {
            await next();
            return;
        }

        var capability = ResolveCapability(context);
        var resource = Truncate(
            context.HttpContext.Request.Path.Value ?? string.Empty,
            OperatorAuditRecord.ResourceMaxLength);
        var audit = new OperatorAuditRecord
        {
            Id = Guid.NewGuid(),
            InvocationId = Guid.NewGuid(),
            UserId = userId,
            CredentialId = credentialId,
            Client = Truncate(
                principal.FindFirstValue(OperatorClaimTypes.Client) ?? "operator",
                OperatorAuditRecord.ClientMaxLength),
            Capability = Truncate(capability, OperatorAuditRecord.CapabilityMaxLength),
            Resource = resource,
            StartedAt = DateTimeOffset.UtcNow
        };

        dbContext.OperatorAuditRecords.Add(audit);
        await dbContext.SaveChangesAsync(context.HttpContext.RequestAborted);
        context.HttpContext.Response.Headers["X-Dorks-Operator-Invocation-Id"] = audit.InvocationId.ToString("D");

        try
        {
            var executed = await next();
            audit.CompletedAt = DateTimeOffset.UtcNow;
            audit.Outcome = executed.Exception is not null && !executed.ExceptionHandled
                ? "Failed"
                : OutcomeFor(executed.Result, context.HttpContext.Response.StatusCode);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch
        {
            audit.CompletedAt = DateTimeOffset.UtcNow;
            audit.Outcome = "Failed";
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private static string ResolveCapability(ActionExecutingContext context)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor descriptor)
        {
            return descriptor.MethodInfo
                .GetCustomAttributes(typeof(OperatorCapabilityAttribute), inherit: true)
                .OfType<OperatorCapabilityAttribute>()
                .SingleOrDefault()?.Name
                ?? descriptor.ActionName;
        }

        return context.ActionDescriptor.DisplayName ?? "operator.unknown";
    }

    private static string OutcomeFor(IActionResult? result, int responseStatusCode)
    {
        var statusCode = result switch
        {
            ObjectResult objectResult when objectResult.StatusCode.HasValue => objectResult.StatusCode.Value,
            StatusCodeResult statusResult => statusResult.StatusCode,
            _ => responseStatusCode
        };

        return statusCode switch
        {
            >= 200 and < 400 => "Succeeded",
            StatusCodes.Status401Unauthorized => "Unauthorized",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "NotFound",
            _ => "Failed"
        };
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
