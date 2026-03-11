using dorks_and_dice_site.Models.Resume;

namespace dorks_and_dice_site.Services.Resume;

public static class ResumePageContentBuilder
{
    public static ResumeViewModel Build()
    {
        return new ResumeViewModel
        {
            Header = new ResumeHeaderViewModel
            {
                FullName = "Kyle W. Barnett",
                Headline = "Information Science Student | Cybersecurity and Software Development",
                Location = "Ponte Vedra Beach, FL",
                HeadshotImageUrl = "~/images/profile/kyle-headshot.jpg",
                HeadshotAltText = "Headshot of Kyle Barnett",
                ResumePdfUrl = "~/files/kyle-resume.pdf",
                ResumePdfFileName = "kyle-resume.pdf",
                LastUpdatedText = "February 2026"
            },
            ContactLinks =
            [
                new ContactLinkViewModel
                {
                    Label = "kbarnett0306@gmail.com",
                    Href = "mailto:kbarnett0306@gmail.com",
                    IconUrl = "~/images/icons/gmail.svg",
                    IconAltText = "Email icon"
                },
                new ContactLinkViewModel
                {
                    Label = "904-803-8980",
                    Href = "tel:+19048038980",
                    IconUrl = "~/images/icons/phone.svg",
                    IconAltText = "Phone icon"
                },
                new ContactLinkViewModel
                {
                    Label = "linkedin.com/in/kyle-barnett03",
                    Href = "https://www.linkedin.com/in/kyle-barnett03/",
                    IconUrl = "~/images/icons/linkedin.png",
                    IconAltText = "LinkedIn icon",
                    OpenInNewTab = true
                },
                new ContactLinkViewModel
                {
                    Label = "github.com/LegendMaster03",
                    Href = "https://github.com/LegendMaster03",
                    IconUrl = "~/images/icons/github.svg",
                    IconAltText = "GitHub icon",
                    IconCssClass = "github-icon",
                    OpenInNewTab = true
                }
            ],
            EducationEntries =
            [
                new EducationEntryViewModel
                {
                    Institution = "University of North Florida, Jacksonville, FL",
                    CardCssClass = "card mb-2",
                    Lines =
                    [
                        new ResumeLineItemViewModel { Text = "Information Science (Bachelor's), Minor in Leadership", CssClass = "mb-1" },
                        new ResumeLineItemViewModel { Text = "Senior currently working toward a bachelor's degree in Information Science.", CssClass = "mb-1" },
                        new ResumeLineItemViewModel { Text = "August 2023 - Present | Expected Graduation: May 1, 2026 | Dean's List | Upsilon Pi Epsilon member", CssClass = "mb-0", IsMuted = true }
                    ]
                },
                new EducationEntryViewModel
                {
                    Institution = "Florida State College at Jacksonville, Jacksonville, FL",
                    CardCssClass = "card mb-2",
                    Lines =
                    [
                        new ResumeLineItemViewModel { Text = "Associate of Arts completed", CssClass = "mb-1" },
                        new ResumeLineItemViewModel { Text = "January 2023 - August 2023 (plus prior study August 2020 - July 2022)", CssClass = "mb-0", IsMuted = true }
                    ]
                },
                new EducationEntryViewModel
                {
                    Institution = "Florida Polytechnic University, Lakeland, FL",
                    CardCssClass = "card",
                    Lines =
                    [
                        new ResumeLineItemViewModel { Text = "August 2022 - December 2022", CssClass = "mb-1", IsMuted = true },
                        new ResumeLineItemViewModel { Text = "Prior study at Florida State College at Jacksonville: August 2020 - July 2022", CssClass = "mb-0", IsMuted = true }
                    ]
                }
            ],
            AwardEntries =
            [
                new AwardEntryViewModel
                {
                    Title = "Upsilon Pi Epsilon (UPE), International Honor Society for the Computing Sciences",
                    CardCssClass = "card mb-2",
                    MetaText = "Issued February 2026 by Upsilon Pi Epsilon | Associated with University of North Florida",
                    Summary = "International Honor Society for the Computing Sciences."
                },
                new AwardEntryViewModel
                {
                    Title = "Dean's List",
                    CardCssClass = "card mb-2",
                    MetaText = "Issued May 2025 by William F. Klostermeyer | Associated with University of North Florida",
                    Summary = "Awarded for strong academic performance."
                },
                new AwardEntryViewModel
                {
                    Title = "Eagle Scout",
                    CardCssClass = "card",
                    MetaText = "Issued August 5, 2020 by Boy Scouts of America",
                    Highlights =
                    [
                        "Held multiple troop leadership roles, including Senior Patrol Leader and Webmaster.",
                        "Completed National Youth Leadership Training and later served as an instructor.",
                        "Applied EDGE leadership methodology: Educate, Demonstrate, Guide, and Enable."
                    ],
                    AdditionalDescription = "As a Boy Scout, I learned the value of strong leadership through multiple Positions of Responsibility: Den Chief, Historian, Web Master, SPL, ASPL, OA Representative, and Junior Assistant Scout Master. I attended NYLT and later returned to staff NYLT. These experiences built practical skills in teamwork, teaching through the EDGE method, patience, social communication, conflict resolution, motivation, and mentoring younger scouts. These lessons continue to shape my professional and personal leadership style."
                }
            ],
            SkillCategories =
            [
                new SkillCategoryViewModel
                {
                    Name = "Industry Knowledge",
                    Description = "Back-end Web Development, Front-end Development, Patch Management, Cybersecurity, Application Development, Multimedia, Game Development, Game Design, Level Design, Technical Support"
                },
                new SkillCategoryViewModel
                {
                    Name = "Tools and Technologies",
                    Description = "Python, JavaScript, C, C#, Java, HTML5, HTML, Unity, Unreal Engine, Unreal Engine 4, Creation Kit, Gamebryo, 3D Printing, Audio Visual (AV) Systems, Computer Building, Technical Support (Windows, Mac, Linux)"
                },
                new SkillCategoryViewModel
                {
                    Name = "Interpersonal Skills",
                    Description = "Leadership, collaboration, mentoring, communication, conflict resolution, and instructional coaching through the EDGE method."
                }
            ],
            ExperienceItems =
            [
                new ExperienceItemViewModel
                {
                    Title = "UNF Cyber Security Team - Web Specialist and Windows Specialist",
                    DateRange = "2024 - Present",
                    CardCssClass = "card mb-2",
                    Highlights =
                    [
                        "Participates in a competitive travel team environment.",
                        "Attends daily practices to optimize methods for complex security use cases.",
                        "Attends lectures from global security experts."
                    ],
                    DetailsAction = "ExperienceCyberSecurityTeam",
                    Featured = true,
                    Tags = ["cybersecurity", "professional"]
                },
                new ExperienceItemViewModel
                {
                    Title = "Technology Services (Personal Business) - Tech Enthusiast",
                    DateRange = "2019 - Present",
                    CardCssClass = "card mb-2",
                    Highlights =
                    [
                        "Performs computer migrations and software upgrades for local clients.",
                        "Assists with data-entry projects and network management tasks."
                    ],
                    DetailsAction = "ExperienceTechnologyServices",
                    Featured = true,
                    Tags = ["professional", "it-support"]
                },
                new ExperienceItemViewModel
                {
                    Title = "Florida Polytechnic University S.I.M. Lab - Student Worker",
                    DateRange = "August 2022 - December 2022",
                    CardCssClass = "card mb-2",
                    Highlights =
                    [
                        "Handled asset management and supported motion-capture capabilities.",
                        "Created a video game for a Game Expo showcase."
                    ],
                    DetailsAction = "ExperienceSimLab",
                    Tags = ["professional", "game-dev"]
                },
                new ExperienceItemViewModel
                {
                    Title = "Wired Works (Network and Audio Visual) - Assistant Audio-Visual Installer",
                    DateRange = "April 2021 - September 2021",
                    CardCssClass = "card mb-2",
                    Highlights =
                    [
                        "Installed internet networks for businesses and homes.",
                        "Configured smart-home automations with platforms like Alexa and Sonos.",
                        "Summer role completed prior to starting college."
                    ],
                    DetailsAction = "ExperienceWiredWorks",
                    Tags = ["professional", "networking"]
                },
                new ExperienceItemViewModel
                {
                    Title = "Skyblivion Global Project - Interior Level Designer",
                    DateRange = "March 2021 - August 2022",
                    CardCssClass = "card",
                    Highlights =
                    [
                        "Completed 11 detailed interior levels with a global development team.",
                        "Tested experimental workloads as the project transitioned to cloud workflows.",
                        "Mentored new team members for faster onboarding success."
                    ],
                    DetailsAction = "ExperienceSkyblivion",
                    Tags = ["professional", "game-dev"]
                },
                new ExperienceItemViewModel
                {
                    Title = "Skywind - Interior Level Designer",
                    DateRange = "December 2021 - 2022",
                    CardCssClass = "card mt-2",
                    DetailsAction = "ExperienceSkywind",
                    Tags = ["professional", "game-dev"]
                }
            ],
            ProjectItems =
            [
                new ProjectItemViewModel
                {
                    Title = "XnGine Framework",
                    Summary = "Personal project: in-progress framework architecture and engineering documentation.",
                    Category = "personal",
                    Action = "XnGine",
                    Featured = true,
                    Tags = ["framework", "engineering"]
                },
                new ProjectItemViewModel
                {
                    Title = "Senior Project",
                    Summary = "Capstone work focused on practical system implementation and delivery.",
                    Category = "professional",
                    Action = "SeniorProject",
                    Featured = true,
                    Tags = ["capstone"]
                },
                new ProjectItemViewModel
                {
                    Title = "UNF Cyber Security Team Work",
                    Summary = "Security practice methodologies, team prep, and competition workflows.",
                    Category = "professional",
                    Action = "CyberSecurityTeam",
                    Tags = ["cybersecurity", "teamwork"]
                },
                new ProjectItemViewModel
                {
                    Title = "Skyblivion Interiors",
                    Summary = "Detailed interior design work and image gallery.",
                    Category = "professional",
                    Action = "Skyblivion",
                    Tags = ["game-dev", "level-design"]
                },
                new ProjectItemViewModel
                {
                    Title = "Skywind Interiors",
                    Summary = "Interior level design work completed for the Skywind project.",
                    Category = "professional",
                    Action = "Skywind",
                    Tags = ["game-dev", "level-design"]
                },
                new ProjectItemViewModel
                {
                    Title = "Technology Services (Personal Business)",
                    Summary = "Client-focused upgrades, migrations, and network support work.",
                    Category = "professional",
                    Action = "TechnologyServices",
                    Tags = ["it-support"]
                },
                new ProjectItemViewModel
                {
                    Title = "Weed Wacker (S.I.M. Lab Expo Prototype)",
                    Summary = "Motion capture, asset support, and game demo creation.",
                    Category = "professional",
                    Action = "SimLabExpo",
                    Tags = ["game-dev", "prototype"]
                },
                new ProjectItemViewModel
                {
                    Title = "D&D Tooling and Campaign Systems",
                    Summary = "Frameworks and tools for long-form collaborative campaigns.",
                    Category = "personal",
                    Action = "DndTools",
                    Featured = true,
                    Tags = ["tooling", "leadership"]
                }
            ],
            LeadershipEntries =
            [
                new LeadershipEntryViewModel
                {
                    Title = "Dungeons & Dragons Game Master",
                    DateRange = "2024 - Present",
                    CardCssClass = "card mb-2",
                    RelatedProjectAction = "DndTools",
                    RelatedProjectLabel = "D&D Tooling and Campaign Systems",
                    Highlights =
                    [
                        "Plans and facilitates recurring sessions, balancing long-term roadmap planning with weekly execution.",
                        "Leads live group communication and public-speaking-style facilitation for collaborative decision-making.",
                        "Resolves interpersonal conflicts and keeps teams aligned under changing priorities and constraints.",
                        "Builds repeatable systems and documentation to improve onboarding, consistency, and delivery quality."
                    ],
                    PrimaryAction = "DndTools",
                    PrimaryActionText = "Open Project"
                }
            ]
        };
    }
}
