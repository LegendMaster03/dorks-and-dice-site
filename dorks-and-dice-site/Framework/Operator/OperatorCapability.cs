namespace dorks_and_dice_site.Framework.Operator;

/// <summary>
/// Declares the stable capability required to call an Operator API action.
/// This is independent of a normal hosted site's route or editor permission.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class OperatorCapabilityAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

public sealed record OperatorCapabilityDescriptor(
    string Name,
    string Method,
    string Route,
    string Description);

public interface IOperatorCapabilityRegistry
{
    IReadOnlyList<OperatorCapabilityDescriptor> GetSiteCapabilities();
}
