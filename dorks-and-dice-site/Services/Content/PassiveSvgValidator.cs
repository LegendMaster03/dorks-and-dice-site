using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace dorks_and_dice_site.Services.Content;

// Keep uploaded vectors passive even when a user opens a media URL as a document.
internal static class PassiveSvgValidator
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";
    private static readonly HashSet<string> Elements = new(StringComparer.Ordinal)
    {
        "svg", "g", "path", "rect", "circle", "ellipse", "line", "polyline", "polygon",
        "title", "desc", "defs", "clipPath", "mask", "linearGradient", "radialGradient",
        "stop", "style", "text", "tspan", "use", "symbol"
    };
    private static readonly HashSet<string> Attributes = new(StringComparer.Ordinal)
    {
        "id", "class", "role", "style", "type", "version", "viewBox", "preserveAspectRatio",
        "x", "y", "x1", "x2", "y1", "y2", "cx", "cy", "r", "rx", "ry", "width", "height",
        "d", "points", "transform", "fill", "fill-rule", "fill-opacity", "stroke", "stroke-width",
        "stroke-linecap", "stroke-linejoin", "stroke-miterlimit", "stroke-dasharray", "stroke-dashoffset",
        "stroke-opacity", "opacity", "clip-path", "clip-rule", "mask", "offset", "stop-color",
        "stop-opacity", "gradientUnits", "gradientTransform", "spreadMethod", "fx", "fy",
        "font-family", "font-size", "font-weight", "text-anchor", "dx", "dy", "href"
    };

    public static bool IsValid(byte[] data)
    {
        try
        {
            using var stream = new MemoryStream(data);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = ContentInputPolicy.MaxAssetUploadBytes
            });
            var document = XDocument.Load(reader);
            if (document.Root?.Name != Svg + "svg"
                || document.DescendantNodes().OfType<XProcessingInstruction>().Any()) return false;
            foreach (var element in document.Root.DescendantsAndSelf())
            {
                if (element.Name.Namespace != Svg || !Elements.Contains(element.Name.LocalName)) return false;
                if (element.Name.LocalName == "style" && !IsPassiveCss(element.Value)) return false;
                foreach (var attribute in element.Attributes())
                {
                    if (attribute.IsNamespaceDeclaration || attribute.Name == XNamespace.Xml + "space") continue;
                    if (attribute.Name.NamespaceName.Length > 0
                        && attribute.Name != XName.Get("href", "http://www.w3.org/1999/xlink")) return false;
                    if (!Attributes.Contains(attribute.Name.LocalName)) return false;
                    if (attribute.Name.LocalName == "href" && !Regex.IsMatch(attribute.Value, @"^#[A-Za-z_][A-Za-z0-9_.-]*$")) return false;
                    if (!IsPassiveCss(attribute.Value)) return false;
                }
            }
            return true;
        }
        catch (XmlException) { return false; }
    }

    private static bool IsPassiveCss(string value) =>
        value.IndexOfAny(['\\', '@', '<', '>']) < 0
        && !Regex.IsMatch(value, @"url\s*\(|expression\s*\(|/\*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
