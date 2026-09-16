using dorks_and_dice_site.Framework.Operator;

namespace dorks_and_dice_site.Services.Operator;

public sealed class OperatorCapabilityRegistry : IOperatorCapabilityRegistry
{
    private static readonly IReadOnlyList<OperatorCapabilityDescriptor> Capabilities =
    [
        new("operator.me", "GET", "/operator/v1/me", "Return the authenticated service-principal identity and effective global roles.", ResponseSchema: "OperatorMeResponse"),
        new("operator.capabilities", "GET", "/operator/v1/capabilities", "Discover Site capabilities and Operator-capable Tools.", ResponseSchema: "OperatorCapabilitiesResponse"),
        new("operator.openapi", "GET", "/operator/v1/openapi.json", "Return the canonical Operator HTTPS/JSON OpenAPI description."),
        new("content.list", "GET", "/operator/v1/content", "List content the principal may edit in the authoring workspace.", ResponseSchema: "OperatorContentListResponse"),
        new("content.get", "GET", "/operator/v1/content/{source}/{slug}", "Retrieve an editable content document and revision history.", ResponseSchema: "OperatorContentDocumentResponse"),
        new("content.create", "POST", "/operator/v1/content", "Create content through the existing content authoring service.", "OperatorContentWriteRequest", "OperatorContentDocumentResponse", StatusCodes.Status201Created),
        new("content.save_revision", "PUT", "/operator/v1/content/{source}/{slug}", "Save a new revision through the existing content authoring service.", "OperatorContentWriteRequest", "OperatorContentDocumentResponse"),
        new("content.preview", "POST", "/operator/v1/content/{source}/{slug}/preview", "Render a candidate content revision without saving it.", "OperatorContentPreviewRequest", "OperatorContentPreviewResponse"),
        new("content.media.list", "GET", "/operator/v1/content/{source}/{slug}/media", "List media attached to editable content.", ResponseSchema: "OperatorMediaListResponse"),
        new("content.media.upload", "POST", "/operator/v1/content/{source}/{slug}/media", "Upload and attach supported media through the existing media service.", "OperatorMediaUploadRequest", "ContentAssetInfo", StatusCodes.Status201Created),
        new("tools.manifest", "GET", "/operator/v1/tools/{slug}/manifest", "Retrieve an opted-in Tool Operator Contract v1 manifest through the Tool Host trust path.", ResponseSchema: "ToolOperatorManifest"),
        new("tools.invoke", "POST", "/operator/v1/tools/{slug}/invoke/{capability}", "Invoke a declared Tool capability through a fixed Operator Contract v1 endpoint.", "ToolCapabilityRequest", "ToolCapabilityResponse")
    ];

    public IReadOnlyList<OperatorCapabilityDescriptor> GetSiteCapabilities() => Capabilities;
}
