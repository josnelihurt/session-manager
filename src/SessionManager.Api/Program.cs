using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Data;
using SessionManager.Api.Models;
using SessionManager.Api.Models.Auth;
using SessionManager.Api.Services;
using StackExchange.Redis;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Bind configuration sections
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<GoogleOptions>(builder.Configuration.GetSection(GoogleOptions.SectionName));
builder.Services.Configure<InvitationOptions>(builder.Configuration.GetSection(InvitationOptions.SectionName));

// Add DbContext
var databaseOptions = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
    ?? new DatabaseOptions { ConnectionString = "" };

if (string.IsNullOrEmpty(databaseOptions.ConnectionString))
{
    Console.Error.WriteLine("ERROR: Database__ConnectionString is required");
    Environment.Exit(1);
}

builder.Services.AddDbContext<SessionManagerDbContext>(options =>
{
    options.UseNpgsql(databaseOptions.ConnectionString);
});

// Add Redis
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

// Add services
builder.Services.AddSingleton<ISessionService, RedisSessionService>();
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IGoogleOAuthService, GoogleOAuthService>();
builder.Services.AddHttpClient<IGoogleOAuthService, GoogleOAuthService>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IInvitationService, InvitationService>();

// Add controllers
builder.Services.AddControllers();

// Configure JSON serialization (not using Native AOT for now)
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    // Disable source generation for now to ensure proper model binding
    // options.SerializerOptions.TypeInfoResolver = AppJsonContext.Default;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

// Configure JSON serialization for controllers
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

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
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

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

// Map controllers
app.MapControllers();

// ==================== AUTH API ENDPOINTS ====================

// GET /api/auth/providers - Get available auth providers
app.MapGet("/api/auth/providers", async (IAuthService authService) =>
{
    var providers = await authService.GetProvidersAsync();
    return Results.Ok(providers);
});

// POST /api/auth/login - Local login
app.MapPost("/api/auth/login", async (
    [FromBody] LoginRequest request,
    HttpRequest httpRequest,
    IAuthService authService,
    IOptions<AuthOptions> authOptions,
    ILogger<Program> logger) =>
{
    var ipAddress = httpRequest.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var userAgent = httpRequest.Headers.UserAgent.ToString();

    var result = await authService.LoginAsync(request, ipAddress, userAgent);

    if (!result.Success)
    {
        return Results.Text(result.Error ?? "Unauthorized", statusCode: 401);
    }

    // Set session cookie
    httpRequest.HttpContext.Response.Cookies.Append(authOptions.Value.CookieName, result.SessionKey!, new CookieOptions
    {
        Domain = authOptions.Value.CookieDomain,
        Path = "/",
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        MaxAge = TimeSpan.FromHours(authOptions.Value.SessionLifetimeHours)
    });

    return Results.Ok(new LoginSuccessResponse(result.User));
});

// POST /api/auth/register - User registration with invitation token
app.MapPost("/api/auth/register", async (
    HttpContext context,
    IAuthService authService,
    IOptions<AuthOptions> authOptions,
    ILogger<Program> logger) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();

    logger.LogInformation("Registration endpoint called with body: {Body}", body);

    if (string.IsNullOrEmpty(body))
    {
        return Results.Text("Request body is empty", statusCode: 400);
    }

    using var jsonDoc = JsonDocument.Parse(body);
    var token = jsonDoc.RootElement.GetProperty("token").GetString();
    var provider = jsonDoc.RootElement.GetProperty("provider").GetString();
    var username = jsonDoc.RootElement.GetProperty("username").GetString();
    var password = jsonDoc.RootElement.GetProperty("password").GetString();

    if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(provider) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
    {
        return Results.Text("Missing required fields", statusCode: 400);
    }

    var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var userAgent = context.Request.Headers.UserAgent.ToString();

    var request = new RegisterRequest(token, provider, username, password);
    var result = await authService.RegisterAsync(request, ipAddress, userAgent);

    if (!result.Success)
    {
        logger.LogWarning("Registration failed: {Error}", result.Error);
        return Results.Text(result.Error ?? "Registration failed", statusCode: 400);
    }

    // Set session cookie
    context.Response.Cookies.Append(authOptions.Value.CookieName, result.SessionKey!, new CookieOptions
    {
        Domain = authOptions.Value.CookieDomain,
        Path = "/",
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        MaxAge = TimeSpan.FromHours(authOptions.Value.SessionLifetimeHours)
    });

    logger.LogInformation("User {Username} registered successfully", username);
    return Results.Ok(new LoginSuccessResponse(result.User));
});

// GET /api/auth/me - Get current user
app.MapGet("/api/auth/me", async (
    HttpRequest request,
    IAuthService authService,
    IOptions<AuthOptions> authOptions) =>
{
    var sessionKey = request.Cookies[authOptions.Value.CookieName];
    if (string.IsNullOrEmpty(sessionKey))
    {
        return Results.Unauthorized();
    }

    var user = await authService.GetCurrentUserAsync(sessionKey);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    return Results.Ok(user);
});

// POST /api/auth/logout - Logout
app.MapPost("/api/auth/logout", async (
    HttpRequest request,
    IAuthService authService,
    IOptions<AuthOptions> authOptions) =>
{
    var sessionKey = request.Cookies[authOptions.Value.CookieName];
    if (!string.IsNullOrEmpty(sessionKey))
    {
        await authService.LogoutAsync(sessionKey);
    }

    request.HttpContext.Response.Cookies.Delete(authOptions.Value.CookieName, new CookieOptions
    {
        Domain = authOptions.Value.CookieDomain,
        Path = "/"
    });

    return Results.Ok(new MessageResponse("Logged out"));
});

// GET /api/test - Test endpoint to verify new endpoints work
app.MapGet("/api/test", () =>
{
    return Results.Ok(new { message = "Test endpoint works!", timestamp = DateTime.UtcNow });
});

// GET /api/debug/hash - Generate BCrypt hash (TEMPORARY - remove after fixing)
app.MapGet("/api/debug/hash", (string password, IPasswordHasher passwordHasher) =>
{
    if (string.IsNullOrEmpty(password))
        return Results.Text("password parameter required", statusCode: 400);

    var hash = passwordHasher.HashPassword(password);
    return Results.Text($"{{ \"password\": \"{password}\", \"hash\": \"{hash}\" }}");
});

// GET /api/auth/login/google - Start Google OAuth flow
app.MapGet("/api/auth/login/google", async (
    [FromQuery] string? invitation,
    IGoogleOAuthService googleService,
    IOptions<AuthOptions> authOptions) =>
{
    var state = Guid.NewGuid().ToString("N");
    var authUrl = googleService.GetAuthorizationUrl(state, invitation);
    return Results.Redirect(authUrl);
});

// GET /api/auth/callback/google - Google OAuth callback
app.MapGet("/api/auth/callback/google", async (
    [FromQuery] string code,
    [FromQuery] string state,
    HttpRequest httpRequest,
    IGoogleOAuthService googleService,
    ISessionService sessionService,
    IOptions<AuthOptions> authOptions,
    SessionManagerDbContext dbContext,
    ILogger<Program> logger) =>
{
    var ipAddress = httpRequest.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var userAgent = httpRequest.Headers.UserAgent.ToString();

    // Exchange code for tokens
    var tokens = await googleService.ExchangeCodeAsync(code);
    if (tokens == null)
    {
        return Results.Redirect($"/login?error={Uri.EscapeDataString("Failed to authenticate with Google")}");
    }

    // Get user info
    var googleUser = await googleService.GetUserInfoAsync(tokens.AccessToken);
    if (googleUser == null || !googleUser.EmailVerified)
    {
        return Results.Redirect($"/login?error={Uri.EscapeDataString("Could not verify Google account")}");
    }

    // Check if user exists
    var user = await dbContext.Users
        .FirstOrDefaultAsync(u =>
            (u.Provider == "google" && u.ProviderId == googleUser.Id) ||
            (u.Email == googleUser.Email));

    // For Google OAuth, we create user if not exists (simplified for initial implementation)
    if (user == null)
    {
        user = new SessionManager.Api.Entities.User
        {
            Username = googleUser.Email.Split('@')[0],
            Email = googleUser.Email,
            Provider = "google",
            ProviderId = googleUser.Id,
            IsActive = true,
            IsSuperAdmin = false // First Google users are not admins
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Created new user {Email} via Google OAuth", user.Email);
    }

    if (!user.IsActive)
    {
        return Results.Redirect($"/login?error={Uri.EscapeDataString("Account is disabled")}");
    }

    // Update last login
    user.LastLoginAt = DateTime.UtcNow;
    await dbContext.SaveChangesAsync();

    // Create session
    var sessionKey = await sessionService.CreateSessionAsync(user.Id, user.Username, user.Email, user.IsSuperAdmin, ipAddress, userAgent);

    // Set session cookie
    httpRequest.HttpContext.Response.Cookies.Append(authOptions.Value.CookieName, sessionKey, new CookieOptions
    {
        Domain = authOptions.Value.CookieDomain,
        Path = "/",
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        MaxAge = TimeSpan.FromHours(authOptions.Value.SessionLifetimeHours)
    });

    // Redirect to dashboard
    return Results.Redirect("/dashboard");
});

// ==================== FORWARD AUTH ENDPOINTS ====================

// GET /auth - ForwardAuth endpoint for Traefik
app.MapGet("/auth", async (
    HttpRequest request,
    HttpResponse response,
    IAuthService authService,
    IOptions<AuthOptions> authOptions,
    ILogger<Program> logger) =>
{
    var cookieName = authOptions.Value.CookieName;
    var sessionKey = request.Cookies[cookieName];

    // No session cookie
    if (string.IsNullOrEmpty(sessionKey))
    {
        logger.LogDebug("ForwardAuth: No session cookie");
        return Results.StatusCode(401);
    }

    // Validate session
    var user = await authService.GetCurrentUserAsync(sessionKey);
    if (user == null)
    {
        logger.LogDebug("ForwardAuth: Invalid session {Session}", sessionKey[..8]);
        return Results.StatusCode(401);
    }

    // Get requested application URL from Traefik headers
    var forwardedHost = request.Headers["X-Forwarded-Host"].FirstOrDefault();
    var applicationUrl = forwardedHost ?? "";

    logger.LogDebug("ForwardAuth: User {User} requesting access to {App}",
        user.Username, applicationUrl);

    // Super admin bypasses permission check
    if (!user.IsSuperAdmin)
    {
        // Check permission for this application
        var hasAccess = await authService.CanAccessApplicationAsync(sessionKey, applicationUrl);
        if (!hasAccess)
        {
            logger.LogWarning("ForwardAuth: User {User} denied access to {App}",
                user.Username, applicationUrl);
            return Results.StatusCode(403);
        }
    }

    // Set response headers for downstream services
    response.Headers.Append("X-Auth-Request-User", user.Username);
    response.Headers.Append("X-Auth-Request-Email", user.Email);
    response.Headers.Append("X-Session-Manager-Id", sessionKey);
    response.Headers.Append("X-User-Is-Admin", user.IsSuperAdmin.ToString().ToLower());

    logger.LogDebug("ForwardAuth: User {User} granted access to {App}",
        user.Username, applicationUrl);

    return Results.Ok();
});

// GET /auth-forward - Lightweight forward auth with application access check
app.MapGet("/auth-forward", async (
    HttpRequest request,
    IAuthService authService,
    IOptions<AuthOptions> authOptions,
    ILogger<Program> logger) =>
{
    var cookieName = authOptions.Value.CookieName;
    var sessionKey = request.Cookies[cookieName];

    if (string.IsNullOrEmpty(sessionKey))
    {
        return Results.StatusCode(401);
    }

    var user = await authService.GetCurrentUserAsync(sessionKey);
    if (user == null)
    {
        return Results.StatusCode(401);
    }

    // Get requested application URL from Traefik headers
    var forwardedHost = request.Headers["X-Forwarded-Host"].FirstOrDefault();
    var applicationUrl = forwardedHost ?? "";

    // Super admin bypasses permission check
    if (!user.IsSuperAdmin)
    {
        // Check permission for this application
        var hasAccess = await authService.CanAccessApplicationAsync(sessionKey, applicationUrl);
        if (!hasAccess)
        {
            logger.LogWarning("ForwardAuth: User {User} denied access to {App}",
                user.Username, applicationUrl);
            return Results.StatusCode(403);
        }
    }

    request.HttpContext.Response.Headers.Append("X-Auth-Request-User", user.Username);
    request.HttpContext.Response.Headers.Append("X-Auth-Request-Email", user.Email);

    return Results.Text("");
});

app.Run();

public partial class Program { }
