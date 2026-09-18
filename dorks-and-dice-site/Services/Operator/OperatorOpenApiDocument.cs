using dorks_and_dice_site.Framework.Operator;

namespace dorks_and_dice_site.Services.Operator;

public static class OperatorOpenApiDocument
{
    public static object Create(IReadOnlyList<OperatorCapabilityDescriptor> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        var paths = capabilities.ToDictionary(
            capability => capability.Route,
            capability => (object)new Dictionary<string, object>
            {
                [capability.Method.ToLowerInvariant()] = BuildOperation(capability)
            },
            StringComparer.Ordinal);

        return new Dictionary<string, object>
        {
            ["openapi"] = "3.1.0",
            ["info"] = new Dictionary<string, object>
            {
                ["title"] = "Dorks & Dice Operator API",
                ["version"] = "1.0"
            },
            ["paths"] = paths,
            ["components"] = new Dictionary<string, object>
            {
                ["securitySchemes"] = new Dictionary<string, object>
                {
                    ["OperatorBearer"] = new Dictionary<string, object>
                    {
                        ["type"] = "http",
                        ["scheme"] = "bearer",
                        ["bearerFormat"] = "ddop_v1"
                    }
                },
                ["schemas"] = BuildSchemas()
            }
        };
    }

    private static object BuildOperation(OperatorCapabilityDescriptor capability) =>
        new Dictionary<string, object>
        {
            ["operationId"] = capability.Name,
            ["summary"] = capability.Description,
            ["security"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["OperatorBearer"] = Array.Empty<string>()
                }
            },
            ["responses"] = new Dictionary<string, object>
            {
                ["200"] = BuildSuccessResponse(capability.Name),
                ["401"] = new Dictionary<string, object>
                {
                    ["description"] = "Invalid, expired, revoked, or missing Operator credential."
                }
            }
        };

    private static object BuildSuccessResponse(string capabilityName)
    {
        var schema = capabilityName switch
        {
            "operator.me" => Ref("OperatorMeResponse"),
            "operator.capabilities" => Ref("OperatorCapabilitiesResponse"),
            "operator.browser_bootstrap" => Ref("OperatorBrowserBootstrapResponse"),
            "operator.openapi" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["additionalProperties"] = true
            },
            _ => throw new InvalidOperationException(
                $"Operator capability '{capabilityName}' is not described by the OpenAPI document.")
        };

        return new Dictionary<string, object>
        {
            ["description"] = "Success.",
            ["content"] = new Dictionary<string, object>
            {
                ["application/json"] = new Dictionary<string, object>
                {
                    ["schema"] = schema
                }
            }
        };
    }

    private static Dictionary<string, object> BuildSchemas() => new(StringComparer.Ordinal)
    {
        ["OperatorMeResponse"] = ObjectSchema(
            new()
            {
                ["userId"] = UuidSchema(),
                ["displayName"] = StringSchema(),
                ["accountKind"] = StringSchema(),
                ["globalRoles"] = ArraySchema(StringSchema())
            },
            "userId", "displayName", "accountKind", "globalRoles"),
        ["OperatorCapabilityDescriptor"] = ObjectSchema(
            new()
            {
                ["name"] = StringSchema(),
                ["method"] = StringSchema(),
                ["route"] = StringSchema(),
                ["description"] = StringSchema()
            },
            "name", "method", "route", "description"),
        ["OperatorCapabilitiesResponse"] = ObjectSchema(
            new()
            {
                ["site"] = ArraySchema(Ref("OperatorCapabilityDescriptor"))
            },
            "site"),
        ["OperatorBrowserBootstrapResponse"] = ObjectSchema(
            new()
            {
                ["bootstrapId"] = UuidSchema(),
                ["bootstrapUrl"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["format"] = "uri-reference"
                },
                ["expiresAt"] = DateTimeSchema()
            },
            "bootstrapId", "bootstrapUrl", "expiresAt")
    };

    private static Dictionary<string, object> Ref(string schemaName) => new()
    {
        ["$ref"] = $"#/components/schemas/{schemaName}"
    };

    private static Dictionary<string, object> ObjectSchema(
        Dictionary<string, object> properties,
        params string[] required) => new()
    {
        ["type"] = "object",
        ["properties"] = properties,
        ["required"] = required,
        ["additionalProperties"] = false
    };

    private static Dictionary<string, object> StringSchema() => new()
    {
        ["type"] = "string"
    };

    private static Dictionary<string, object> UuidSchema() => new()
    {
        ["type"] = "string",
        ["format"] = "uuid"
    };

    private static Dictionary<string, object> DateTimeSchema() => new()
    {
        ["type"] = "string",
        ["format"] = "date-time"
    };

    private static Dictionary<string, object> ArraySchema(object items) => new()
    {
        ["type"] = "array",
        ["items"] = items
    };
}
