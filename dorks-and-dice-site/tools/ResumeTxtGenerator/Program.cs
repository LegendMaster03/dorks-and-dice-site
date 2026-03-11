using System.Text;
using dorks_and_dice_site.Models.Resume;
using dorks_and_dice_site.Services.Resume;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: ResumeTxtGenerator <mvc-project-directory>");
    return 1;
}

var projectDir = args[0];
var outputPath = Path.Combine(projectDir, "wwwroot", "files", "kyle-resume.txt");
var model = ResumePageContentBuilder.Build(projectDir);
var text = ConvertModelToPlainText(model);

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllText(outputPath, text + Environment.NewLine, new UTF8Encoding(false));

Console.WriteLine($"Generated: {outputPath}");
return 0;

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
