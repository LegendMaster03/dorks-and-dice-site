namespace dorks_and_dice_site.Models.Resume;

public class ResumeViewModel
{
    public ResumeHeaderViewModel Header { get; set; } = new();
    public List<ContactLinkViewModel> ContactLinks { get; set; } = [];
    public List<EducationEntryViewModel> EducationEntries { get; set; } = [];
    public List<AwardEntryViewModel> AwardEntries { get; set; } = [];
    public List<SkillCategoryViewModel> SkillCategories { get; set; } = [];
    public List<ExperienceItemViewModel> ExperienceItems { get; set; } = [];
    public List<ProjectItemViewModel> ProjectItems { get; set; } = [];
    public List<LeadershipEntryViewModel> LeadershipEntries { get; set; } = [];
}
