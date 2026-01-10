using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Services;
using SessionManager.Api.Models;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Bind configuration sections
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));

// Add services
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = sp.GetRequiredService<IOptions<RedisOptions>>().Value ?? new RedisOptions();
    var configuration = ConfigurationOptions.Parse(config.ConnectionString);
    configuration.AbortOnConnectFail = false;
    configuration.ConnectRetry = 3;
    configuration.ConnectTimeout = 10000;
    configuration.AsyncTimeout = 10000;
    configuration.SyncTimeout = 10000;
    configuration.ReconnectRetryPolicy = new ExponentialRetry(1000);
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddSingleton<ISessionService, RedisSessionService>();

// CORS
var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
var allowedOrigins = new List<string> { corsOptions.FrontendUrl };

if (builder.Environment.IsDevelopment())
{
    allowedOrigins.Add("http://localhost:5173");
}

if (corsOptions.AdditionalOrigins.Length > 0)
{
    allowedOrigins.AddRange(corsOptions.AdditionalOrigins);
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins.ToArray())
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowFrontend");

// GET /api/sessions - Get all sessions
app.MapGet("/api/sessions", async (ISessionService sessionService) =>
{
    try
    {
        var sessions = await sessionService.GetAllSessionsAsync();
        var sessionDtos = sessions.Select(s => new SessionDto(
            s.SessionId,
            s.CookiePrefix,
            s.TtlMilliseconds,
            s.ExpiresAt?.ToString("o"),
            FormatRemainingTime(s.TtlMilliseconds),
            s.FullKey
        ));
        return Results.Ok(new SessionsResponse(true, sessionDtos, sessions.Count));
    }
    catch (Exception)
    {
        return Results.Problem("Failed to retrieve sessions", statusCode: 500);
    }
});

// DELETE /api/sessions/{fullKey} - Delete a specific session
app.MapDelete("/api/sessions/{fullKey}", async (string fullKey, ISessionService sessionService) =>
{
    try
    {
        fullKey = Uri.UnescapeDataString(fullKey);
        var result = await sessionService.DeleteSessionAsync(fullKey);
        var message = result ? "Session deleted successfully" : "Session not found";
        return Results.Ok(new DeleteResponse(result, message));
    }
    catch (Exception)
    {
        return Results.Problem("Failed to delete session", statusCode: 500);
    }
});

// DELETE /api/sessions - Delete all sessions
app.MapDelete("/api/sessions", async (ISessionService sessionService) =>
{
    try
    {
        var count = await sessionService.DeleteAllSessionsAsync();
        return Results.Ok(new DeleteAllResponse(true, $"Deleted {count} session(s)", count));
    }
    catch (Exception)
    {
        return Results.Problem("Failed to delete sessions", statusCode: 500);
    }
});

static string FormatRemainingTime(long ttlMs)
{
    if (ttlMs < 0)
        return "Expired";

    var timeSpan = TimeSpan.FromMilliseconds(ttlMs);

    if (timeSpan.TotalHours >= 1)
        return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
    if (timeSpan.TotalMinutes >= 1)
        return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
    return $"{timeSpan.Seconds}s";
}

app.Run();

public partial class Program { }
