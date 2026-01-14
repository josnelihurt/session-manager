using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Services.Auth;
using SessionManager.Api.Services.Sessions;
using SessionManager.Api.Services;

namespace SessionManager.Api.Endpoints;

public static class ForwardAuthEndpoints
{
    public static void MapForwardAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var configuration = app.ServiceProvider.GetRequiredService<IConfiguration>();

        // GET /auth - ForwardAuth endpoint for Traefik
        app.MapGet("/auth", async (
            HttpRequest request,
            HttpResponse response,
            IAuthService authService,
            IOptions<AuthOptions> authOptions,
            ILogger<Program> logger) =>
        {
            // Get requested application URL and path from Traefik headers
            var forwardedHost = request.Headers[SessionManagerConstants.HttpHeaders.XForwardedHost].FirstOrDefault()
                ?? request.Headers[SessionManagerConstants.HttpHeaders.XOriginalHost].FirstOrDefault()
                ?? request.Headers[SessionManagerConstants.HttpHeaders.Host].FirstOrDefault();
            var applicationUrl = forwardedHost ?? "";

            // Get the request path from X-Forwarded-Uri or X-Original-URI
            var forwardedUri = request.Headers[SessionManagerConstants.HttpHeaders.XForwardedUri].FirstOrDefault()
                ?? request.Headers[SessionManagerConstants.HttpHeaders.XOriginalUri].FirstOrDefault()
                ?? "";

            // Extract the path portion (without query string)
            var requestPath = ExtractPath(forwardedUri);

            logger.LogDebug("ForwardAuth: Checking {App} with path {Path}", applicationUrl, requestPath);

            // Check if this path should skip authentication (from configuration)
            var skipPathsConfiguration = configuration["SkipAuthPaths"] ?? "";
            if (!string.IsNullOrEmpty(skipPathsConfiguration) && SkipPathMatcher.ShouldSkipPath(requestPath, skipPathsConfiguration))
            {
                logger.LogDebug("ForwardAuth: Path {Path} matches skip patterns - allowing without auth", requestPath);
                return Results.Ok();
            }

            var sessionKey = request.Cookies[SessionManagerConstants.SessionCookieName];

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
            response.Headers.Append(SessionManagerConstants.HttpHeaders.XAuthRequestUser, user.Username);
            response.Headers.Append(SessionManagerConstants.HttpHeaders.XAuthRequestEmail, user.Email);
            response.Headers.Append(SessionManagerConstants.HttpHeaders.XSessionManagerId, sessionKey);
            response.Headers.Append(SessionManagerConstants.HttpHeaders.XUserIsAdmin, user.IsSuperAdmin.ToString().ToLower());

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
            var sessionKey = request.Cookies[SessionManagerConstants.SessionCookieName];

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

            request.HttpContext.Response.Headers.Append(SessionManagerConstants.HttpHeaders.XAuthRequestUser, user.Username);
            request.HttpContext.Response.Headers.Append(SessionManagerConstants.HttpHeaders.XAuthRequestEmail, user.Email);

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

    private static string ExtractPath(string uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return "/";
        }

        // Remove query string and fragment
        var queryIndex = uri.IndexOf('?');
        if (queryIndex > 0)
        {
            uri = uri[..queryIndex];
        }

        var fragmentIndex = uri.IndexOf('#');
        if (fragmentIndex > 0)
        {
            uri = uri[..fragmentIndex];
        }

        // Parse the URI to get the path
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri))
        {
            return parsedUri.AbsolutePath;
        }

        // If not a valid URI, treat it as a path directly
        return uri.StartsWith('/') ? uri : "/" + uri;
    }
}
