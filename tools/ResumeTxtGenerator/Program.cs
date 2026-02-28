using System.Net;
using System.Text;
using System.Text.RegularExpressions;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: ResumeTxtGenerator <mvc-project-directory>");
    return 1;
}

var projectDir = args[0];
var resumeViewPath = Path.Combine(projectDir, "Views", "Home", "Resume.cshtml");
var outputPath = Path.Combine(projectDir, "wwwroot", "files", "kyle-resume.txt");

if (!File.Exists(resumeViewPath))
{
    Console.Error.WriteLine($"Resume view not found: {resumeViewPath}");
    return 1;
}

var source = File.ReadAllText(resumeViewPath);
var text = ConvertRazorToPlainText(source);

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllText(outputPath, text + Environment.NewLine, new UTF8Encoding(false));

Console.WriteLine($"Generated: {outputPath}");
return 0;

static string ConvertRazorToPlainText(string source)
{
    // Remove Razor code blocks and directives first.
    source = Regex.Replace(source, @"@\{[\s\S]*?\}", string.Empty, RegexOptions.Multiline);
    source = Regex.Replace(source, @"@\w+\([^\)]*\)", string.Empty, RegexOptions.Multiline);

    // Keep line breaks for common block-level elements.
    source = Regex.Replace(source, @"</(h1|h2|h3|h4|h5|h6|p|section|div|ul|ol|li|br|tr|table)>", "\n", RegexOptions.IgnoreCase);
    source = Regex.Replace(source, @"<li[^>]*>", "- ", RegexOptions.IgnoreCase);

    // Preserve link text + URL in plain text.
    source = Regex.Replace(
        source,
        @"<a[^>]*href=""([^""]+)""[^>]*>(.*?)</a>",
        m =>
        {
            var href = WebUtility.HtmlDecode(m.Groups[1].Value);
            var label = StripTags(m.Groups[2].Value).Trim();
            return string.IsNullOrWhiteSpace(label) ? href : $"{label} ({href})";
        },
        RegexOptions.IgnoreCase | RegexOptions.Singleline);

    // Remove remaining HTML tags.
    source = StripTags(source);
    source = WebUtility.HtmlDecode(source);

    var lines = source
        .Split('\n')
        .Select(line => Regex.Replace(line, @"\s+", " ").Trim())
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .ToList();

    // De-duplicate immediate repeats from Razor formatting.
    var skipLines = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "|",
        "All",
        "Professional",
        "Personal",
        "Open Project",
        "Detailed experience view"
    };

    var cleaned = new List<string>(lines.Count);
    foreach (var line in lines)
    {
        if (skipLines.Contains(line))
        {
            continue;
        }

        if (cleaned.Count == 0 || !string.Equals(cleaned[^1], line, StringComparison.Ordinal))
        {
            cleaned.Add(line);
        }
    }

    return string.Join(Environment.NewLine, cleaned);
}

static string StripTags(string input) =>
    Regex.Replace(input, @"<[^>]+>", string.Empty, RegexOptions.Singleline);
