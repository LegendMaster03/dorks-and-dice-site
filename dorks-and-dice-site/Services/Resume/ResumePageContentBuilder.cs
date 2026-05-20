using System.Text.Json;
using dorks_and_dice_site.Models.Resume;

namespace dorks_and_dice_site.Services.Resume;

public static class ResumePageContentBuilder
{
    private static readonly string[] RelativePathSegments = ["Content", "Resume", "resume.json"];

    public static ResumeViewModel Build(string? contentRootPath = null)
    {
        var jsonPath = ResolveJsonPath(contentRootPath);
        var json = File.ReadAllText(jsonPath);

        var model = JsonSerializer.Deserialize<ResumeViewModel>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            });

        if (model is null)
        {
            throw new InvalidOperationException($"Failed to deserialize resume content from '{jsonPath}'.");
        }

        NormalizeCollections(model);
        ResumeContentValidator.ValidateOrThrow(model, jsonPath);
        return model;
    }

    private static string ResolveJsonPath(string? contentRootPath)
    {
        if (!string.IsNullOrWhiteSpace(contentRootPath))
        {
            var explicitPath = Path.Combine(contentRootPath, Path.Combine(RelativePathSegments));
            if (File.Exists(explicitPath))
            {
                return explicitPath;
            }

            throw new FileNotFoundException(
                $"Resume content JSON was not found at '{explicitPath}'.",
                explicitPath);
        }

        var candidatePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, Path.Combine(RelativePathSegments)),
            Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(RelativePathSegments))
        };

        var resolved = candidatePaths.FirstOrDefault(File.Exists);
        if (resolved is not null)
        {
            return resolved;
        }

        throw new FileNotFoundException(
            $"Resume content JSON was not found. Checked: {string.Join("; ", candidatePaths)}");
    }

    private static void NormalizeCollections(ResumeViewModel model)
    {
        model.ContactLinks ??= [];
        model.EducationEntries ??= [];
        model.AwardEntries ??= [];
        model.SkillCategories ??= [];
        model.ExperienceItems ??= [];
        model.ProjectItems ??= [];
        model.LeadershipEntries ??= [];

        foreach (var educationEntry in model.EducationEntries)
        {
            educationEntry.Lines ??= [];
        }

        foreach (var awardEntry in model.AwardEntries)
        {
            awardEntry.Highlights ??= [];
        }

        foreach (var experienceItem in model.ExperienceItems)
        {
            experienceItem.Highlights ??= [];
            experienceItem.Tags ??= [];
        }

        foreach (var projectItem in model.ProjectItems)
        {
            projectItem.Tags ??= [];
        }

        foreach (var leadershipEntry in model.LeadershipEntries)
        {
            leadershipEntry.Highlights ??= [];
        }
    }
}
