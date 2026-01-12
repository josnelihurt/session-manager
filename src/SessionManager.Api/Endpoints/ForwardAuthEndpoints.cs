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
                var html = UnauthorizedPageBuilder.BuildUnauthorizedPage();
                response.StatusCode = 401;
                response.ContentType = "text/html; charset=utf-8";
                await response.WriteAsync(html);
                return Results.Empty;
            }

            // Validate session
            var user = await authService.GetCurrentUserAsync(sessionKey);
            if (user == null)
            {
                logger.LogDebug("ForwardAuth: Invalid session {Session}", sessionKey[..8]);
                var html = UnauthorizedPageBuilder.BuildUnauthorizedPage();
                response.StatusCode = 401;
                response.ContentType = "text/html; charset=utf-8";
                await response.WriteAsync(html);
                return Results.Empty;
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

                    // Return custom 403 HTML page
                    var html = ForbiddenPageBuilder.BuildForbiddenPage(null);
                    response.StatusCode = 403;
                    response.ContentType = "text/html; charset=utf-8";
                    await response.WriteAsync(html);
                    return Results.Empty;
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
            HttpResponse response,
            IAuthService authService,
            IOptions<AuthOptions> authOptions,
            ILogger<Program> logger) =>
        {
            var cookieName = authOptions.Value.CookieName;
            var sessionKey = request.Cookies[cookieName];

            if (string.IsNullOrEmpty(sessionKey))
            {
                logger.LogDebug("ForwardAuth: No session cookie");
                var html = ForbiddenPageBuilder.BuildForbiddenPage(null);
                response.StatusCode = 403;
                response.ContentType = "text/html; charset=utf-8";
                await response.WriteAsync(html);
                return Results.Empty;
            }

            var user = await authService.GetCurrentUserAsync(sessionKey);
            if (user == null)
            {
                logger.LogDebug("ForwardAuth: Invalid session");
                var html = ForbiddenPageBuilder.BuildForbiddenPage(null);
                response.StatusCode = 403;
                response.ContentType = "text/html; charset=utf-8";
                await response.WriteAsync(html);
                return Results.Empty;
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

                    // Return custom 403 HTML page
                    var html = ForbiddenPageBuilder.BuildForbiddenPage(null);
                    response.StatusCode = 403;
                    response.ContentType = "text/html; charset=utf-8";
                    await response.WriteAsync(html);
                    return Results.Empty;
                }
            }

            request.HttpContext.Response.Headers.Append("X-Auth-Request-User", user.Username);
            request.HttpContext.Response.Headers.Append("X-Auth-Request-Email", user.Email);

            return Results.Text("");
        });

        // GET /api/static/403.png - Serve the wizard image
        app.MapGet("/api/static/403.png", async (HttpResponse response, CancellationToken cancellationToken) =>
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = "SessionManager.Api.Assets.403.png";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                return Results.NotFound("Image not found in assembly");
            }

            response.ContentType = "image/png";
            response.StatusCode = 200;
            await stream.CopyToAsync(response.Body, cancellationToken);
            return Results.Empty;
        });
    }
}
