namespace dorks_and_dice_site.Framework.Operator;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class OperatorCapabilityAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

public sealed record OperatorCapabilityDescriptor(
    string Name,
    string Method,
    string Route,
    string Description,
    string? RequestSchema = null,
    string? ResponseSchema = null,
    int SuccessStatusCode = StatusCodes.Status200OK);

public interface IOperatorCapabilityRegistry
{
    IReadOnlyList<OperatorCapabilityDescriptor> GetSiteCapabilities();
}
