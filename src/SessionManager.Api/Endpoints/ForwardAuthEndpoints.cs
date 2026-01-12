using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Services;

namespace SessionManager.Api.Endpoints;

public static class ForwardAuthEndpoints
{
    public static void MapForwardAuthEndpoints(this IEndpointRouteBuilder app)
    {
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
    }
}
