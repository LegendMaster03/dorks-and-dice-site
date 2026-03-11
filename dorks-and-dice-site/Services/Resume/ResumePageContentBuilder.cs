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

        ValidateRequiredContent(model, jsonPath);
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

    private static void ValidateRequiredContent(ResumeViewModel model, string sourcePath)
    {
        if (model.Header is null)
        {
            throw new InvalidOperationException($"Resume header is required in '{sourcePath}'.");
        }

        if (string.IsNullOrWhiteSpace(model.Header.FullName))
        {
            throw new InvalidOperationException($"Resume header fullName is required in '{sourcePath}'.");
        }

        if (model.ContactLinks is null || model.ContactLinks.Count == 0)
        {
            throw new InvalidOperationException($"At least one contact link is required in '{sourcePath}'.");
        }

        if (model.EducationEntries is null || model.EducationEntries.Count == 0)
        {
            throw new InvalidOperationException($"At least one education entry is required in '{sourcePath}'.");
        }

        if (model.AwardEntries is null || model.AwardEntries.Count == 0)
        {
            throw new InvalidOperationException($"At least one award entry is required in '{sourcePath}'.");
        }

        if (model.SkillCategories is null || model.SkillCategories.Count == 0)
        {
            throw new InvalidOperationException($"At least one skill category is required in '{sourcePath}'.");
        }

        if (model.ExperienceItems is null || model.ExperienceItems.Count == 0)
        {
            throw new InvalidOperationException($"At least one experience item is required in '{sourcePath}'.");
        }

        if (model.ProjectItems is null || model.ProjectItems.Count == 0)
        {
            throw new InvalidOperationException($"At least one project item is required in '{sourcePath}'.");
        }

        if (model.LeadershipEntries is null || model.LeadershipEntries.Count == 0)
        {
            throw new InvalidOperationException($"At least one leadership entry is required in '{sourcePath}'.");
        }
    }
}
