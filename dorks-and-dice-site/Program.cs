using dorks_and_dice_site.Services.Resume;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IResumeContentService, ResumeContentService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
// List of professional domains
var professionalDomains = new[] { "k-barnett.com", "kyle-barnett.com", "kylebarnett.com" };

// Restrict professional domains to /resume and its subpaths
app.Use(async (context, next) =>
{
    var host = context.Request.Host.Host.ToLower();
    // Normalize host by removing leading 'www.' if present
    if (host.StartsWith("www."))
    {
        host = host.Substring(4);
    }
    var path = context.Request.Path.ToString().ToLower();
    bool isProDomain = professionalDomains.Contains(host);
    bool isResumePath = path == "/resume" || path.StartsWith("/resume/");
    // Allow static asset paths
    bool isStaticAsset = path.StartsWith("/css/") || path.StartsWith("/js/") || path.StartsWith("/lib/") || path.StartsWith("/images/") || path.StartsWith("/files/") || path.StartsWith("/favicon") || path.StartsWith("/robots.txt");

    // Set a flag for professional domain branding
    context.Items["ForceKyleBarnettBranding"] = isProDomain;

    if (isProDomain)
    {
        // Restrict professional domains to /resume, its subpaths, or static assets
        if (!isResumePath && !isStaticAsset)
        {
            context.Response.Redirect("/resume");
            return;
        }
    }
    await next();
});
app.UseRouting();
app.UseStatusCodePagesWithReExecute("/Home/NotFoundPage");

app.UseAuthorization();

app.MapStaticAssets();

app.MapGet("/health", () => Results.Text("OK", "text/plain"));

app.MapControllerRoute(
    name: "resume",
    pattern: "resume",
    defaults: new { controller = "Resume", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
