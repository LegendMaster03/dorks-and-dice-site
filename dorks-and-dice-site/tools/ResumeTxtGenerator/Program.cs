using System.Text;
using dorks_and_dice_site.Models.Resume;
using dorks_and_dice_site.Services.Resume;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  ResumeTxtGenerator <mvc-project-directory>");
    Console.Error.WriteLine("  ResumeTxtGenerator --validate <mvc-project-directory>");
    return 1;
}

var validateOnly = args.Length == 2 && string.Equals(args[0], "--validate", StringComparison.OrdinalIgnoreCase);
var projectDir = validateOnly ? args[1] : args[0];
if (string.IsNullOrWhiteSpace(projectDir))
{
    Console.Error.WriteLine("Project directory is required.");
    return 1;
}

try
{
    var model = ResumePageContentBuilder.Build(projectDir);
    if (validateOnly)
    {
        Console.WriteLine($"Validated: {Path.Combine(projectDir, "Content", "Resume", "resume.json")}");
        return 0;
    }

    var outputPath = Path.Combine(projectDir, "wwwroot", "files", "kyle-resume.txt");
    var text = ConvertModelToPlainText(model);

    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    File.WriteAllText(outputPath, text + Environment.NewLine, new UTF8Encoding(false));

    Console.WriteLine($"Generated: {outputPath}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Resume content processing failed: {ex.Message}");
    return 1;
}

static string ConvertModelToPlainText(ResumeViewModel model)
{
    var lines = new List<string>
    {
        model.Header.FullName,
        model.Header.Headline,
        model.Header.Location,
        $"Download PDF: {model.Header.ResumePdfFileName} ({model.Header.ResumePdfUrl})",
        $"Last updated: {model.Header.LastUpdatedText}",
        "Contact"
    };

    lines.AddRange(model.ContactLinks.Select(link => $"{link.Label} ({link.Href})"));

    lines.Add("Education");
    foreach (var educationEntry in model.EducationEntries)
    {
        lines.Add(educationEntry.Institution);
        lines.AddRange(educationEntry.Lines.Select(line => line.Text));
    }

    lines.Add("Honors & Awards");
    foreach (var awardEntry in model.AwardEntries)
    {
        lines.Add(awardEntry.Title);
        if (!string.IsNullOrWhiteSpace(awardEntry.MetaText))
        {
            lines.Add(awardEntry.MetaText);
        }

        if (!string.IsNullOrWhiteSpace(awardEntry.Summary))
        {
            lines.Add(awardEntry.Summary);
        }

        lines.AddRange(awardEntry.Highlights.Select(highlight => $"- {highlight}"));

        if (!string.IsNullOrWhiteSpace(awardEntry.AdditionalDescription))
        {
            lines.Add(awardEntry.AdditionalDescription);
        }
    }

    lines.Add("Skills");
    foreach (var skillCategory in model.SkillCategories)
    {
        lines.Add(skillCategory.Name);
        lines.Add(skillCategory.Description);
    }

    lines.Add("Experience");
    foreach (var experienceItem in model.ExperienceItems)
    {
        lines.Add(experienceItem.Title);
        lines.Add(experienceItem.DateRange);
        lines.AddRange(experienceItem.Highlights.Select(highlight => $"- {highlight}"));
    }

    lines.Add("Projects");
    foreach (var projectItem in model.ProjectItems)
    {
        lines.Add(projectItem.Title);
        lines.Add(projectItem.Summary);
    }

    lines.Add("Leadership Experience");
    foreach (var leadershipEntry in model.LeadershipEntries)
    {
        lines.Add(leadershipEntry.Title);
        lines.Add(leadershipEntry.DateRange);
        if (!string.IsNullOrWhiteSpace(leadershipEntry.RelatedProjectLabel))
        {
            lines.Add($"Related project: {leadershipEntry.RelatedProjectLabel}");
        }

        lines.AddRange(leadershipEntry.Highlights.Select(highlight => $"- {highlight}"));
    }

    return string.Join(
        Environment.NewLine,
        lines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim()));
}
