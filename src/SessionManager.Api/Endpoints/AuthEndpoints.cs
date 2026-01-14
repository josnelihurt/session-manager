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

        // POST /api/auth/login - Local login with OTP support
        app.MapPost("/api/auth/login", async (
            [FromBody] LoginWithOtpRequest request,
            HttpRequest httpRequest,
            IAuthService authService,
            IOtpService otpService,
            IOptions<AuthOptions> authOptions,
            ILogger<Program> logger) =>
        {
            var ipAddress = httpRequest.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = httpRequest.Headers.UserAgent.ToString();

            // For local users, validate credentials first
            var user = await authService.ValidateCredentialsAsync(request.Username, request.Password);
            if (user == null)
            {
                return Results.Text("Invalid credentials", statusCode: 401);
            }

            // Local users require OTP
            if (user.Provider == SessionManagerConstants.LocalProvider)
            {
                // If OTP code is provided, verify it
                if (!string.IsNullOrEmpty(request.OtpCode))
                {
                    var otpValid = await otpService.VerifyAndConsumeOtpAsync(user.Email, request.OtpCode);
                    if (!otpValid)
                    {
                        return Results.Text("Invalid or expired verification code", statusCode: 401);
                    }

                    // OTP is valid, create session
                    var result = await authService.CreateSessionForUserAsync(user.Id, ipAddress, userAgent);
                    if (!result.Success)
                    {
                        return Results.Text(result.Error ?? "Failed to create session", statusCode: 401);
                    }

                    // Set session cookie
                    httpRequest.HttpContext.Response.Cookies.Append(SessionManagerConstants.SessionCookieName, result.SessionKey!, new CookieOptions
                    {
                        Domain = SessionManagerConstants.CookieDomain,
                        Path = "/",
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Lax,
                        MaxAge = TimeSpan.FromHours(authOptions.Value.SessionLifetimeHours)
                    });

                    logger.LogInformation("User {Username} logged in with OTP successfully", user.Username);
                    return Results.Ok(new LoginSuccessResponse(result.User));
                }
                else
                {
                    // No OTP provided, generate and send OTP
                    await otpService.GenerateOtpAsync(user.Email);
                    logger.LogInformation("OTP generated for user {Username}", user.Username);

                    // Return response indicating OTP is required
                    return Results.Ok(new LoginOtpResponse(
                        RequiresOtp: true,
                        Message: "A verification code has been sent to your email",
                        Email: user.Email,
                        User: null
                    ));
                }
            }

            // For non-local users (should not reach here as Google users use OAuth flow)
            // But if somehow they do, create session directly
            var directResult = await authService.CreateSessionForUserAsync(user.Id, ipAddress, userAgent);
            if (!directResult.Success)
            {
                return Results.Text(directResult.Error ?? "Failed to create session", statusCode: 401);
            }

            httpRequest.HttpContext.Response.Cookies.Append(authOptions.Value.CookieName, directResult.SessionKey!, new CookieOptions
            {
                Domain = authOptions.Value.CookieDomain,
                Path = "/",
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromHours(authOptions.Value.SessionLifetimeHours)
            });

            return Results.Ok(new LoginOtpResponse(
                RequiresOtp: false,
                Message: null,
                Email: null,
                User: directResult.User
            ));
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

        // POST /api/auth/logout - Logout (API logout for AJAX requests)
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

        // GET /api/auth/logout - Logout (Redirects to Auth0 for full logout)
        app.MapGet("/api/auth/logout", async (
            HttpRequest request,
            IAuthService authService,
            IOptions<AuthOptions> authOptions,
            IOptions<GoogleOptions> googleOptions,
            IOptions<Auth0Options> auth0Options) =>
        {
            var sessionKey = request.Cookies[authOptions.Value.CookieName];
            string? provider = null;

            if (!string.IsNullOrEmpty(sessionKey))
            {
                var user = await authService.GetCurrentUserAsync(sessionKey);
                provider = user?.Provider;
                await authService.LogoutAsync(sessionKey);
            }

            request.HttpContext.Response.Cookies.Delete(authOptions.Value.CookieName, new CookieOptions
            {
                Domain = authOptions.Value.CookieDomain,
                Path = "/"
            });

            // If user logged in with OAuth provider, redirect to provider's logout
            if (provider == SessionManagerConstants.Auth0Provider)
            {
                var domain = auth0Options.Value.Domain.StartsWith("http")
                    ? auth0Options.Value.Domain
                    : $"https://{auth0Options.Value.Domain}";

                var returnTo = Uri.EscapeDataString($"{request.Scheme}://{request.Host}{SessionManagerConstants.Routes.Login}");
                // Add federated parameter to logout from all identity providers
                var logoutUrl = $"{domain}/v2/logout?client_id={auth0Options.Value.ClientId}&returnTo={returnTo}&federated";
                return Results.Redirect(logoutUrl);
            }

            // Default: redirect to login page
            return Results.Redirect(SessionManagerConstants.Routes.Login);
        });

        // GET /api/test - Test endpoint
        app.MapGet("/api/test", () =>
        {
            return Results.Ok(new { message = "Test endpoint works!", timestamp = DateTime.UtcNow });
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
            string originalState = state;
            if (!string.IsNullOrEmpty(state) && state.Contains('|'))
            {
                var parts = state.Split('|');
                if (parts.Length == 2)
                {
                    originalState = parts[0];
                    invitationToken = parts[1];
                    logger.LogInformation("Extracted invitation token from state");
                }
            }

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
                return Results.Redirect($"{SessionManagerConstants.Routes.Login}?error={Uri.EscapeDataString("Could not verify Google account")}");
            }

            // Check if user exists
            var user = await dbContext.Users
                .FirstOrDefaultAsync(u =>
                    (u.Provider == SessionManagerConstants.GoogleProvider && u.ProviderId == googleUser.Id) ||
                    (u.Email == googleUser.Email));

            // For Google OAuth, we create user if not exists
            if (user == null)
            {
                // If invitation token is provided, validate it first
                Models.Invitations.InvitationDto? invitationDto = null;
                if (!string.IsNullOrEmpty(invitationToken))
                {
                    try
                    {
                        invitationDto = await invitationService.GetByTokenAsync(invitationToken);
                        if (invitationDto == null || invitationDto.IsUsed)
                        {
                            return Results.Redirect($"/login?error={Uri.EscapeDataString("Invalid or expired invitation token")}");
                        }
                        logger.LogInformation("Validated invitation for {Email} with {Count} pre-assigned roles",
                            googleUser.Email, invitationDto.PreAssignedRoles?.Length ?? 0);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to validate invitation token");
                        return Results.Redirect($"/login?error={Uri.EscapeDataString("Invalid invitation token")}");
                    }
                }

                // Create user
                user = new User
                {
                    Username = googleUser.Email.Split('@')[0],
                    Email = googleUser.Email,
                    Provider = SessionManagerConstants.GoogleProvider,
                    ProviderId = googleUser.Id,
                    IsActive = true,
                    IsSuperAdmin = false
                };
                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync();
                logger.LogInformation("Created new user {Email} via Google OAuth", user.Email);

                // Mark invitation as used if present
                if (invitationDto != null)
                {
                    await invitationService.MarkAsUsedAsync(invitationDto.Id, user.Id);
                    logger.LogInformation("Invitation {Token} marked as used", invitationToken.Substring(0, 8) + "...");
                }

                // Assign pre-configured roles from invitation (if any)
                if (invitationDto != null && invitationDto.PreAssignedRoles != null && invitationDto.PreAssignedRoles.Length > 0)
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
                }
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
            httpRequest.HttpContext.Response.Cookies.Append(SessionManagerConstants.SessionCookieName, sessionKey, new CookieOptions
            {
                Domain = SessionManagerConstants.CookieDomain,
                Path = "/",
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromHours(authOptions.Value.SessionLifetimeHours)
            });

            // Redirect to dashboard
            return Results.Redirect(SessionManagerConstants.Routes.Dashboard);
        });
    }
}
