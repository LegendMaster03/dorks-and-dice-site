using dorks_and_dice_site.Models.Resume;

namespace dorks_and_dice_site.Services.Resume;

public static class ResumeContentValidator
{
    public static void ValidateOrThrow(ResumeViewModel model, string sourcePath)
    {
        var errors = new List<string>();

        if (model.Header is null)
        {
            errors.Add("header is required.");
        }
        else
        {
            Require(model.Header.FullName, "header.fullName", errors);
            Require(model.Header.Headline, "header.headline", errors);
            Require(model.Header.Location, "header.location", errors);
        }

        RequireList(model.ContactLinks, "contactLinks", errors);
        for (var i = 0; i < model.ContactLinks.Count; i++)
        {
            var item = model.ContactLinks[i];
            Require(item.Label, $"contactLinks[{i}].label", errors);
            Require(item.Href, $"contactLinks[{i}].href", errors);
        }

        RequireList(model.EducationEntries, "educationEntries", errors);
        for (var i = 0; i < model.EducationEntries.Count; i++)
        {
            var item = model.EducationEntries[i];
            Require(item.Institution, $"educationEntries[{i}].institution", errors);
            RequireList(item.Lines, $"educationEntries[{i}].lines", errors);
        }

        RequireList(model.AwardEntries, "awardEntries", errors);
        for (var i = 0; i < model.AwardEntries.Count; i++)
        {
            var item = model.AwardEntries[i];
            Require(item.Title, $"awardEntries[{i}].title", errors);
        }

        RequireList(model.SkillCategories, "skillCategories", errors);
        for (var i = 0; i < model.SkillCategories.Count; i++)
        {
            var item = model.SkillCategories[i];
            Require(item.Name, $"skillCategories[{i}].name", errors);
            Require(item.Description, $"skillCategories[{i}].description", errors);
        }

        RequireList(model.ExperienceItems, "experienceItems", errors);
        for (var i = 0; i < model.ExperienceItems.Count; i++)
        {
            var item = model.ExperienceItems[i];
            Require(item.Title, $"experienceItems[{i}].title", errors);
            Require(item.DateRange, $"experienceItems[{i}].dateRange", errors);
        }

        RequireList(model.ProjectItems, "projectItems", errors);
        for (var i = 0; i < model.ProjectItems.Count; i++)
        {
            var item = model.ProjectItems[i];
            Require(item.Title, $"projectItems[{i}].title", errors);
            Require(item.Summary, $"projectItems[{i}].summary", errors);
            Require(item.Category, $"projectItems[{i}].category", errors);
            Require(item.Action, $"projectItems[{i}].action", errors);
        }

        RequireList(model.LeadershipEntries, "leadershipEntries", errors);
        for (var i = 0; i < model.LeadershipEntries.Count; i++)
        {
            var item = model.LeadershipEntries[i];
            Require(item.Title, $"leadershipEntries[{i}].title", errors);
            Require(item.DateRange, $"leadershipEntries[{i}].dateRange", errors);
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Resume content validation failed for '{sourcePath}':{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors)}");
        }
    }

    private static void Require(string? value, string fieldPath, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldPath} is required.");
        }
    }

    private static void RequireList<T>(IReadOnlyCollection<T>? values, string fieldPath, List<string> errors)
    {
        if (values is null || values.Count == 0)
        {
            errors.Add($"{fieldPath} must contain at least one item.");
        }
    }
}
