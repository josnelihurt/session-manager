using SessionManager.Api.Endpoints;
using SessionManager.Api.Extensions;
using SessionManager.Api.Services.Applications;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddSessionManagerServices(builder.Configuration, builder.Environment);

var app = builder.Build();

// Configure URLs to listen on port 8080
app.Urls.Add("http://*:8080");

app.UseCors("AllowFrontend");

// Seed applications from configuration on startup
using (var scope = app.Services.CreateScope())
{
    var applicationService = scope.ServiceProvider.GetRequiredService<IApplicationService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var allowedApps = builder.Configuration["AllowedApplications"] ?? "";

    if (!string.IsNullOrEmpty(allowedApps))
    {
        var appUrls = allowedApps.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (appUrls.Length > 0)
        {
            try
            {
                await applicationService.SeedFromConfigAsync(appUrls);
                logger.LogInformation("Seeded {Count} applications from configuration", appUrls.Length);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to seed applications from configuration");
            }
        }
    }
}

// Map controllers and endpoints
app.MapControllers();
app.MapAuthEndpoints();
app.MapAuth0Endpoints();
app.MapForwardAuthEndpoints();

app.Run();

public partial class Program { }
