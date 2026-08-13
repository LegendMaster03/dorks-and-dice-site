using System.Text;
using System.Text.Encodings.Web;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Services.Content;

public sealed class SiteModeArchitectureDirectiveRenderer : IContentDirectiveRenderer
{
    private readonly ISiteModeArchitectureSummaryService _architectureSummaryService;

    public SiteModeArchitectureDirectiveRenderer(ISiteModeArchitectureSummaryService architectureSummaryService)
    {
        _architectureSummaryService = architectureSummaryService;
    }

    public string Name => "site-mode-architecture";

    public string Render()
    {
        var summary = _architectureSummaryService.GetSummary();
        var html = new StringBuilder();

        html.AppendLine("<h2 class=\"h5 mt-4\">Live Mode Matrix</h2>");
        html.AppendLine("<p>This public summary is generated from the same site mode model that the application uses at runtime. It shows the architectural boundaries without exposing internal host lists, file paths, or exact route probes.</p>");
        html.AppendLine("<div class=\"table-responsive\"><table class=\"table table-sm align-middle\"><thead><tr><th scope=\"col\">Mode</th><th scope=\"col\">Purpose</th><th scope=\"col\">Homepage</th><th scope=\"col\">Presentation</th><th scope=\"col\">Isolation</th><th scope=\"col\">Articles</th></tr></thead><tbody>");
        foreach (var mode in summary.Modes)
        {
            html.Append("<tr><th scope=\"row\">").Append(Encode(mode.Name)).Append("</th><td>")
                .Append(Encode(mode.PublicIdentity)).Append("</td><td>")
                .Append(Encode(mode.Homepage)).Append("</td><td>Mode-specific branding and stylesheet resolution with shared fallback behavior.</td><td>")
                .Append(Encode(mode.RouteOwnership)).Append("</td><td>")
                .Append(Encode(mode.ArticleBehavior)).AppendLine("</td></tr>");
        }
        html.AppendLine("</tbody></table></div>");

        html.AppendLine("<h2 class=\"h5 mt-4\">Representative Access Rules</h2>");
        html.AppendLine("<p>These checks are summarized from representative route ownership probes. They show the boundary behavior without publishing exact internal paths.</p>");
        html.AppendLine("<div class=\"table-responsive\"><table class=\"table table-sm align-middle\"><thead><tr><th scope=\"col\">Surface</th>");
        foreach (var mode in summary.Modes)
        {
            html.Append("<th scope=\"col\">").Append(Encode(mode.Name)).Append("</th>");
        }
        html.AppendLine("</tr></thead><tbody>");

        foreach (var probe in summary.RouteProbes)
        {
            html.Append("<tr><th scope=\"row\">").Append(Encode(probe.Purpose)).Append("</th>");
            foreach (var mode in summary.Modes)
            {
                var allowed = probe.AllowedByMode.TryGetValue(mode.SiteMode, out var isAllowed) && isAllowed;
                html.Append("<td><span class=\"badge ")
                    .Append(allowed ? "text-bg-success" : "text-bg-secondary")
                    .Append("\">")
                    .Append(allowed ? "Allowed" : "Blocked")
                    .Append("</span></td>");
            }
            html.AppendLine("</tr>");
        }

        html.AppendLine("</tbody></table></div>");
        return html.ToString();
    }

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);
}
