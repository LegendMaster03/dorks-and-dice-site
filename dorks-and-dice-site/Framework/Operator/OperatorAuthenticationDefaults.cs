namespace dorks_and_dice_site.Framework.Operator;

/// <summary>
/// Current Operator bearer scheme and versioned token prefix. The public
/// identifiers retain deployment-specific naming pending framework extraction.
/// </summary>
public static class OperatorAuthenticationDefaults
{
    public const string Scheme = "OperatorBearer";
    public const string TokenPrefix = "ddop_v1_";
}

public static class OperatorClaimTypes
{
    public const string CredentialId = "dorks-and-dice:operator-credential-id";
    public const string Client = "dorks-and-dice:operator-client";
}
