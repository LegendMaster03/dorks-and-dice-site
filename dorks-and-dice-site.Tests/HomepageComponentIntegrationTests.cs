using System.Net;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

public sealed class HomepageComponentIntegrationTests
{
    [Fact]
    public async Task ProfessionalHomepageRendersExistingExperienceAndProjectCollections()
    {
        using var factory = new PublishedContentWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using (var scope = factory.Services.CreateScope())
        {
            var authoring = scope.ServiceProvider.GetRequiredService<IContentAuthoringService>();
            var model = authoring.GetNew("External");
            model.Document.Id = "professional-home";
            model.Document.Slug = "professional-home";
            model.Document.TagsText = ContentTags.Homepage;
            model.Document.VisibleModesSelection = [BuiltInSiteModes.Professional.Id];
            model.Document.Body = """
                ::: {.resume-section .mb-4 #experience-section}
                ## Experience {.h4 .resume-section-title}

                {{content-collection context="experience" presentation="professional-experience" order="seniorproject,experiencecybersecurityteam"}}
                :::

                ::: {.resume-section .mb-4 #projects-section}
                ## Projects {.h4 .resume-section-title}

                {{content-collection context="project" presentation="professional-projects" featured-first="true"}}
                :::
                """;
            await authoring.CreateAsync(model.Document);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://kylebarnett.com/");
        request.Headers.Host = "kylebarnett.com";
        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"experience-section\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Safe Future Foundation - Full-Stack Developer", html, StringComparison.Ordinal);
        Assert.Contains("Cybersecurity Team", html, StringComparison.Ordinal);
        Assert.Contains("id=\"projectFilters\"", html, StringComparison.Ordinal);
        Assert.Contains("Personal Multi-Mode Website", html, StringComparison.Ordinal);
    }
}
