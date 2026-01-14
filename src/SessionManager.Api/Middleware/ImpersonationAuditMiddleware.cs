using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SessionManager.Api.Configuration;
using SessionManager.Api.Data;
using SessionManager.Api.Entities;
using SessionManager.Api.Models.Auth;
using SessionManager.Api.Services.Sessions;

namespace SessionManager.Api.Middleware;

public class ImpersonationAuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ImpersonationAuditMiddleware> _logger;

    public ImpersonationAuditMiddleware(
        RequestDelegate next,
        IServiceProvider serviceProvider,
        ILogger<ImpersonationAuditMiddleware> logger)
    {
        _next = next;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sessionKey = context.Request.Cookies[SessionManagerConstants.SessionCookieName];

        if (!string.IsNullOrEmpty(sessionKey))
        {
            // Create scope for scoped services
            using var scope = _serviceProvider.CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<SessionManagerDbContext>();

            var sessionData = await sessionService.GetSessionAsync(sessionKey);

            if (sessionData != null && sessionData.IsImpersonated && sessionData.ImpersonationId.HasValue)
            {
                // Store session data in HttpContext for later use
                context.Items["ImpersonationData"] = sessionData;

                // Capture request details
                var originalBodyStream = context.Response.Body;

                using var memoryStream = new MemoryStream();
                context.Response.Body = memoryStream;

                try
                {
                    await _next(context);

                    // Log after response is generated
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    await memoryStream.CopyToAsync(originalBodyStream);

                    // Log the action
                    await LogActionAsync(context, sessionData, dbContext, null);
                }
                catch (Exception ex)
                {
                    // Log the error action
                    await LogActionAsync(context, sessionData, dbContext, ex);
                    throw;
                }
                finally
                {
                    context.Response.Body = originalBodyStream;
                }

                return;
            }
        }

        await _next(context);
    }

    private async Task LogActionAsync(
        HttpContext context,
        SessionData sessionData,
        SessionManagerDbContext dbContext,
        Exception? exception)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;

            // Skip certain paths
            var path = request.Path.Value ?? "";
            if (path.Contains("/api/static/") || path.Contains("/health"))
            {
                return;
            }

            var impersonationSession = await dbContext.ImpersonationSessions
                .FirstOrDefaultAsync(s => s.Id == sessionData.ImpersonationId!.Value);

            if (impersonationSession == null || impersonationSession.EndedAt != null)
            {
                return; // Session already ended or not found
            }

            var auditLog = new ImpersonationAuditLog
            {
                ImpersonationSessionId = impersonationSession.Id,
                Action = DetermineAction(request, response, exception),
                ResourceType = DetermineResourceType(path),
                ResourceId = ExtractResourceId(path),
                HttpMethod = request.Method,
                Endpoint = $"{request.Method} {path}",
                ResponseStatusCode = exception == null ? response.StatusCode : null,
                IpAddress = GetIpAddress(context),
                UserAgent = request.Headers["User-Agent"].ToString(),
                RequestBodyHash = await HashRequestBodyAsync(request)
            };

            dbContext.ImpersonationAuditLogs.Add(auditLog);
            await dbContext.SaveChangesAsync();

            _logger.LogDebug("Logged impersonation action: {Action} for session {SessionId}",
                auditLog.Action, impersonationSession.Id);
        }
        catch (Exception ex)
        {
            // Don't throw - logging failures shouldn't break the application
            _logger.LogError(ex, "Failed to log impersonation audit entry");
        }
    }

    private static string DetermineAction(HttpRequest request, HttpResponse response, Exception? exception)
    {
        if (exception != null)
        {
            return "error";
        }

        var status = response.StatusCode;
        return status switch
        {
            >= 200 and < 300 => "api_call",
            >= 400 and < 500 => "client_error",
            >= 500 => "server_error",
            _ => "unknown"
        };
    }

    private static string? DetermineResourceType(string path)
    {
        if (path.StartsWith("/api/users")) return "user";
        if (path.StartsWith("/api/sessions")) return "session";
        if (path.StartsWith("/api/applications")) return "application";
        if (path.StartsWith("/api/invitations")) return "invitation";
        if (path.StartsWith("/api/impersonate")) return "impersonation";
        if (path.StartsWith("/api/auth")) return "auth";
        return null;
    }

    private static string? ExtractResourceId(string path)
    {
        var segments = path.Split('/');
        // Try to find a GUID in the path
        foreach (var segment in segments)
        {
            if (Guid.TryParse(segment, out _))
            {
                return segment;
            }
        }
        return null;
    }

    private static string? GetIpAddress(HttpContext context)
    {
        var forwardedIp = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedIp))
        {
            return forwardedIp.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static async Task<string?> HashRequestBodyAsync(HttpRequest request)
    {
        try
        {
            if (!request.Body.CanSeek)
            {
                return null;
            }

            request.Body.Position = 0;
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            if (string.IsNullOrEmpty(body))
            {
                return null;
            }

            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(body);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }
}
