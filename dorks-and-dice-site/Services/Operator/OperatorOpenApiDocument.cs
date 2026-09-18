using dorks_and_dice_site.Framework.Operator;

namespace dorks_and_dice_site.Services.Operator;

public static class OperatorOpenApiDocument
{
    public static object Create(IReadOnlyList<OperatorCapabilityDescriptor> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        var paths = capabilities
            .GroupBy(capability => capability.Route, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (object)group.ToDictionary(
                    capability => capability.Method.ToLowerInvariant(),
                    capability => BuildOperation(capability),
                    StringComparer.Ordinal),
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

    private static object BuildOperation(OperatorCapabilityDescriptor capability)
    {
        var operation = new Dictionary<string, object>
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
            ["responses"] = BuildResponses(capability)
        };

        var pathParameters = RouteParameters(capability.Route).ToArray();
        if (pathParameters.Length > 0)
        {
            operation["parameters"] = pathParameters.Select(name => (object)new Dictionary<string, object>
            {
                ["name"] = name,
                ["in"] = "path",
                ["required"] = true,
                ["schema"] = StringSchema()
            }).ToArray();
        }

        if (!string.IsNullOrWhiteSpace(capability.RequestSchema))
        {
            operation["requestBody"] = new Dictionary<string, object>
            {
                ["required"] = true,
                ["content"] = JsonContent(Ref(capability.RequestSchema!))
            };
        }

        return operation;
    }

    private static object BuildResponses(OperatorCapabilityDescriptor capability)
    {
        var success = new Dictionary<string, object>
        {
            ["description"] = capability.SuccessStatusCode == StatusCodes.Status201Created
                ? "Created."
                : "Success."
        };
        if (!string.IsNullOrWhiteSpace(capability.ResponseSchema))
        {
            success["content"] = JsonContent(Ref(capability.ResponseSchema!));
        }

        return new Dictionary<string, object>
        {
            [capability.SuccessStatusCode.ToString()] = success,
            ["400"] = new Dictionary<string, object> { ["description"] = "Invalid request." },
            ["401"] = new Dictionary<string, object> { ["description"] = "Invalid or missing Operator credential." },
            ["403"] = new Dictionary<string, object> { ["description"] = "The authenticated service principal lacks the required Site authority." },
            ["404"] = new Dictionary<string, object> { ["description"] = "The requested resource is not available to this principal." },
            ["409"] = new Dictionary<string, object> { ["description"] = "The requested mutation conflicts with current state." }
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
                ["description"] = StringSchema(),
                ["requestSchema"] = StringSchema(),
                ["responseSchema"] = StringSchema(),
                ["successStatusCode"] = IntegerSchema()
            },
            "name", "method", "route", "description", "successStatusCode"),
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
                ["bootstrapUrl"] = StringSchema(),
                ["expiresAt"] = DateTimeSchema()
            },
            "bootstrapId", "bootstrapUrl", "expiresAt"),
        ["OperatorContentListItem"] = ObjectSchema(
            new()
            {
                ["sourceKey"] = StringSchema(),
                ["id"] = StringSchema(),
                ["slug"] = StringSchema(),
                ["title"] = StringSchema(),
                ["summary"] = StringSchema(),
                ["revisionId"] = IntegerSchema("int64"),
                ["isListed"] = BooleanSchema(),
                ["tags"] = ArraySchema(StringSchema()),
                ["visibleModes"] = ArraySchema(StringSchema())
            },
            "sourceKey", "id", "slug", "title", "summary", "revisionId", "isListed", "tags", "visibleModes"),
        ["OperatorContentListResponse"] = ArraySchema(Ref("OperatorContentListItem")),
        ["OperatorContentRevision"] = ObjectSchema(
            new()
            {
                ["revisionId"] = IntegerSchema("int64"),
                ["parentRevisionId"] = IntegerSchema("int64"),
                ["createdUtc"] = DateTimeSchema()
            },
            "revisionId", "createdUtc"),
        ["OperatorContentDocumentResponse"] = ObjectSchema(
            new()
            {
                ["sourceKey"] = StringSchema(),
                ["id"] = StringSchema(),
                ["slug"] = StringSchema(),
                ["revisionId"] = IntegerSchema("int64"),
                ["isListed"] = BooleanSchema(),
                ["metadataJson"] = StringSchema(),
                ["tags"] = ArraySchema(StringSchema()),
                ["visibleModes"] = ArraySchema(StringSchema()),
                ["bodyFormat"] = StringSchema(),
                ["body"] = StringSchema(),
                ["history"] = ArraySchema(Ref("OperatorContentRevision"))
            },
            "sourceKey", "id", "slug", "revisionId", "isListed", "metadataJson", "tags", "visibleModes", "bodyFormat", "body", "history"),
        ["OperatorContentWriteRequest"] = ObjectSchema(
            new()
            {
                ["id"] = StringSchema(),
                ["slug"] = StringSchema(),
                ["expectedRevisionId"] = IntegerSchema("int64"),
                ["isListed"] = BooleanSchema(),
                ["metadataJson"] = StringSchema(),
                ["tags"] = ArraySchema(StringSchema()),
                ["visibleModes"] = ArraySchema(StringSchema()),
                ["bodyFormat"] = StringSchema(),
                ["body"] = StringSchema()
            },
            "id", "slug", "metadataJson", "tags", "visibleModes", "bodyFormat", "body"),
        ["OperatorContentPreviewRequest"] = ObjectSchema(
            new()
            {
                ["bodyFormat"] = StringSchema(),
                ["body"] = StringSchema()
            },
            "bodyFormat", "body"),
        ["OperatorContentPreviewResponse"] = ObjectSchema(
            new() { ["html"] = StringSchema() },
            "html"),
        ["OperatorMediaUploadRequest"] = ObjectSchema(
            new()
            {
                ["fileName"] = StringSchema(),
                ["mediaType"] = StringSchema(),
                ["base64Data"] = StringSchema()
            },
            "fileName", "mediaType", "base64Data"),
        ["ContentAssetInfo"] = ObjectSchema(
            new()
            {
                ["assetKey"] = StringSchema(),
                ["fileName"] = StringSchema(),
                ["mediaType"] = StringSchema(),
                ["length"] = IntegerSchema("int64"),
                ["sha256"] = StringSchema(),
                ["createdUtc"] = DateTimeSchema(),
                ["url"] = StringSchema(),
                ["markdownReference"] = StringSchema(),
                ["relationship"] = StringSchema(),
                ["sourceKey"] = StringSchema(),
                ["isAttached"] = BooleanSchema()
            },
            "assetKey", "fileName", "mediaType", "length", "sha256", "createdUtc", "url", "markdownReference", "sourceKey", "isAttached"),
        ["OperatorMediaListResponse"] = ArraySchema(Ref("ContentAssetInfo")),
    };

    private static IEnumerable<string> RouteParameters(string route) =>
        route.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => segment.Length > 2 && segment[0] == '{' && segment[^1] == '}')
            .Select(segment => segment[1..^1]);

    private static Dictionary<string, object> JsonContent(object schema) => new()
    {
        ["application/json"] = new Dictionary<string, object>
        {
            ["schema"] = schema
        }
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

    private static Dictionary<string, object> StringSchema() => new() { ["type"] = "string" };
    private static Dictionary<string, object> UuidSchema() => new() { ["type"] = "string", ["format"] = "uuid" };
    private static Dictionary<string, object> DateTimeSchema() => new() { ["type"] = "string", ["format"] = "date-time" };
    private static Dictionary<string, object> BooleanSchema() => new() { ["type"] = "boolean" };
    private static Dictionary<string, object> IntegerSchema(string? format = null)
    {
        var schema = new Dictionary<string, object> { ["type"] = "integer" };
        if (!string.IsNullOrWhiteSpace(format)) schema["format"] = format;
        return schema;
    }
    private static Dictionary<string, object> ArraySchema(object items) => new()
    {
        ["type"] = "array",
        ["items"] = items
    };
}
