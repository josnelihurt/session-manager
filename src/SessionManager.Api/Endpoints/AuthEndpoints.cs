using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Data;
using SessionManager.Api.Entities;
using SessionManager.Api.Models;
using SessionManager.Api.Models.Auth;
using SessionManager.Api.Services;
using System.Text.Json;

namespace SessionManager.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
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
            IOptions<AuthOptions> authOptions) =>
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

        // GET /api/test - Test endpoint
        app.MapGet("/api/test", () =>
        {
            return Results.Ok(new { message = "Test endpoint works!", timestamp = DateTime.UtcNow });
        });

        // GET /api/debug/hash - Generate BCrypt hash (TEMPORARY)
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

            // For Google OAuth, we create user if not exists
            if (user == null)
            {
                user = new User
                {
                    Username = googleUser.Email.Split('@')[0],
                    Email = googleUser.Email,
                    Provider = "google",
                    ProviderId = googleUser.Id,
                    IsActive = true,
                    IsSuperAdmin = false
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
    }
}
