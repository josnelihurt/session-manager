using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Data;
using SessionManager.Api.Entities;
using SessionManager.Api.Models.Auth;
using SessionManager.Api.Services;
using System.Security.Cryptography;

namespace SessionManager.Api.Endpoints;

public static class Auth0Endpoints
{
    public static void MapAuth0Endpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/auth/login/auth0 - Start Auth0 OAuth flow
        app.MapGet("/api/auth/login/auth0", async (
            IAuth0Service auth0Service,
            IInvitationService invitationService,
            [FromQuery] string? invitation,
            [FromQuery] bool forceLogin = false) =>
        {
            var state = GenerateState();
            // isRegistration=true when invitation token is present (new user sign-up)
            var isRegistration = !string.IsNullOrEmpty(invitation);
            string? emailHint = null;

            // Get email from invitation to pre-fill Auth0 form
            if (!string.IsNullOrEmpty(invitation))
            {
                var invitationDto = await invitationService.GetByTokenAsync(invitation);
                if (invitationDto != null)
                {
                    emailHint = invitationDto.Email;
                }
            }

            var authUrl = auth0Service.GetAuthorizationUrl(state, invitation, forceLogin, isRegistration, emailHint);
            return Results.Redirect(authUrl);
        });

        // GET /api/auth/auth0/login-url - Get Auth0 login URL (for frontend AJAX)
        app.MapGet("/api/auth/auth0/login-url", async (
            IAuth0Service auth0Service,
            IInvitationService invitationService,
            [FromQuery] string? invitationToken,
            [FromQuery] bool forceLogin = false) =>
        {
            var state = GenerateState();
            // isRegistration=true when invitation token is present (new user sign-up)
            var isRegistration = !string.IsNullOrEmpty(invitationToken);
            string? emailHint = null;

            // Get email from invitation to pre-fill Auth0 form
            if (!string.IsNullOrEmpty(invitationToken))
            {
                var invitationDto = await invitationService.GetByTokenAsync(invitationToken);
                if (invitationDto != null)
                {
                    emailHint = invitationDto.Email;
                }
            }

            var loginUrl = auth0Service.GetAuthorizationUrl(state, invitationToken, forceLogin, isRegistration, emailHint);
            return Results.Ok(new { loginUrl });
        });

        // GET /api/auth/callback/auth0 - Auth0 OAuth callback
        app.MapGet("/api/auth/callback/auth0", async (
            [FromQuery] string code,
            [FromQuery] string state,
            HttpRequest httpRequest,
            IAuth0Service auth0Service,
            IInvitationService invitationService,
            ISessionService sessionService,
            IOptions<AuthOptions> authOptions,
            SessionManagerDbContext dbContext,
            ILogger<Program> logger) =>
        {
            var ipAddress = httpRequest.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = httpRequest.Headers.UserAgent.ToString();

            // Extract invitation token from state if present
            string? invitationToken = null;
            if (!string.IsNullOrEmpty(state) && state.Contains('|'))
            {
                var parts = state.Split('|');
                if (parts.Length == 2)
                {
                    invitationToken = parts[0];
                    logger.LogInformation("Extracted invitation token from state");
                }
            }

            // Exchange code for tokens
            var (idToken, accessToken) = await auth0Service.ExchangeCodeForTokensAsync(code);
            if (idToken == null || accessToken == null)
            {
                return Results.BadRequest(new { error = "Failed to authenticate with Auth0" });
            }

            // Get user info from Auth0
            var auth0User = await auth0Service.GetUserInfoAsync(accessToken);
            if (auth0User == null)
            {
                return Results.BadRequest(new { error = "Could not get Auth0 user information" });
            }

            // Check if user already exists by ProviderId or Email
            var user = await dbContext.Users
                .FirstOrDefaultAsync(u =>
                    (u.Provider == "auth0" && u.ProviderId == auth0User.UserId) ||
                    (u.Email == auth0User.Email));

            if (user != null)
            {
                // User exists - verify they are using Auth0 provider
                if (user.Provider != "auth0")
                {
                    logger.LogWarning("User {Email} already exists with provider {Provider}, cannot use Auth0",
                        auth0User.Email, user.Provider);
                    return Results.BadRequest(new { error = $"An account with this email already exists using {user.Provider} provider." });
                }

                // Existing Auth0 user - update last login and sync roles
                user.LastLoginAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
                await SyncRolesToAuth0Async(user, dbContext, auth0Service, logger);
                logger.LogInformation("Existing user {Email} logged in via Auth0", user.Email);
            }
            else
            {
                // New user - require invitation
                if (string.IsNullOrEmpty(invitationToken))
                {
                    logger.LogWarning("Auth0 registration attempt without invitation token for {Email}", auth0User.Email);
                    return Results.BadRequest(new { error = "New users must be invited to register with Auth0." });
                }

                // Validate invitation
                var invitationDto = await invitationService.GetByTokenAsync(invitationToken);
                if (invitationDto == null || invitationDto.IsUsed)
                {
                    logger.LogWarning("Invalid or expired invitation token for {Email}", auth0User.Email);
                    return Results.BadRequest(new { error = "Invalid or expired invitation." });
                }

                // Check if invitation is for Auth0 provider
                if (invitationDto.Provider != "auth0")
                {
                    logger.LogWarning("Invitation {Token} is for provider {Provider}, not auth0",
                        invitationToken.Substring(0, 8) + "...", invitationDto.Provider);
                    return Results.BadRequest(new { error = "This invitation is not valid for Auth0 login." });
                }

                // Create new user
                user = new User
                {
                    Username = auth0User.Email.Split('@')[0],
                    Email = auth0User.Email,
                    Provider = "auth0",
                    ProviderId = auth0User.UserId,
                    IsActive = true,
                    IsSuperAdmin = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync();
                logger.LogInformation("Created new user {Email} via Auth0 OAuth", user.Email);

                // Mark invitation as used
                await invitationService.MarkAsUsedAsync(invitationDto.Id, user.Id);
                logger.LogInformation("Invitation {Token} marked as used", invitationToken.Substring(0, 8) + "...");

                // Assign pre-configured roles from invitation
                if (invitationDto.PreAssignedRoles != null && invitationDto.PreAssignedRoles.Length > 0)
                {
                    foreach (var roleIdStr in invitationDto.PreAssignedRoles)
                    {
                        if (Guid.TryParse(roleIdStr, out var roleId))
                        {
                            var userRole = new UserRole
                            {
                                UserId = user.Id,
                                RoleId = roleId,
                                CreatedAt = DateTime.UtcNow
                            };
                            dbContext.UserRoles.Add(userRole);
                        }
                    }
                    await dbContext.SaveChangesAsync();
                    logger.LogInformation("Assigned {Count} pre-configured roles to user {Username}",
                        invitationDto.PreAssignedRoles.Length, user.Username);

                    // Sync roles to Auth0
                    await SyncRolesToAuth0Async(user, dbContext, auth0Service, logger);
                }
            }

            if (!user.IsActive)
            {
                return Results.BadRequest(new { error = "Account is disabled" });
            }

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

            // Redirect to dashboard on success
            return Results.Redirect("/dashboard");
        });
    }

    private static async Task SyncRolesToAuth0Async(
        User user,
        SessionManagerDbContext dbContext,
        IAuth0Service auth0Service,
        ILogger logger)
    {
        try
        {
            // Get user's roles from session-manager
            var userRoles = await dbContext.UserRoles
                .Include(ur => ur.Role)
                .ThenInclude(r => r.Application)
                .Where(ur => ur.UserId == user.Id)
                .ToListAsync();

            // Build application roles dictionary for Auth0
            var applicationRoles = new Dictionary<string, List<string>>();

            foreach (var userRole in userRoles)
            {
                var appName = userRole.Role.Application.Name;
                var roleName = userRole.Role.Name;

                if (!applicationRoles.ContainsKey(appName))
                {
                    applicationRoles[appName] = new List<string>();
                }
                applicationRoles[appName].Add(roleName);
            }

            // Update Auth0 user metadata
            await auth0Service.UpdateUserRolesAsync(user.ProviderId!, applicationRoles);
            logger.LogInformation("Synced {Count} roles to Auth0 for user {Email}",
                applicationRoles.Count, user.Email);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync roles to Auth0 for user {Email}", user.Email);
        }
    }

    private static string GenerateState()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "");
    }
}
