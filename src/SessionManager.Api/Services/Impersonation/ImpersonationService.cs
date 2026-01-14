using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SessionManager.Api.Data;
using SessionManager.Api.Entities;
using SessionManager.Api.Models.Auth;
using SessionManager.Api.Models.Impersonation;
using SessionManager.Api.Services.Sessions;
using SessionManager.Api.Configuration;

namespace SessionManager.Api.Services.Impersonation;

public class ImpersonationService : IImpersonationService
{
    private readonly SessionManagerDbContext _context;
    private readonly ISessionService _sessionService;
    private readonly ILogger<ImpersonationService> _logger;
    private readonly ImpersonationOptions _options;

    public ImpersonationService(
        SessionManagerDbContext context,
        ISessionService sessionService,
        ILogger<ImpersonationService> logger,
        IOptions<ImpersonationOptions> options)
    {
        _context = context;
        _sessionService = sessionService;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<ImpersonationResult> StartAsync(
        Guid impersonatorUserId,
        string impersonatorSessionKey,
        Guid targetUserId,
        string reason,
        int durationMinutes,
        string ipAddress,
        string userAgent)
    {
        // 1. Validate permissions
        var canImpersonate = await CanImpersonateAsync(impersonatorUserId, targetUserId);
        if (!canImpersonate.CanImpersonate)
        {
            _logger.LogWarning("Impersonation denied: Admin {AdminId} trying to impersonate {TargetId}. Reason: {Reason}",
                impersonatorUserId, targetUserId, canImpersonate.Reason);
            return new ImpersonationResult(false, canImpersonate.Reason, null, null, null);
        }

        // 2. Check for existing active impersonation
        var existingSession = await _context.ImpersonationSessions
            .FirstOrDefaultAsync(s => s.ImpersonatorId == impersonatorUserId && s.EndedAt == null);

        if (existingSession != null)
        {
            return new ImpersonationResult(false, "You already have an active impersonation session", null, null, null);
        }

        // 3. Get target user
        var targetUser = await _context.Users.FindAsync(targetUserId);
        if (targetUser == null || !targetUser.IsActive)
        {
            return new ImpersonationResult(false, "Target user not found or disabled", null, null, null);
        }

        // 4. Get impersonator info
        var impersonator = await _context.Users.FindAsync(impersonatorUserId);
        if (impersonator == null)
        {
            return new ImpersonationResult(false, "Impersonator not found", null, null, null);
        }

        // 5. Calculate expiry
        var duration = Math.Min(durationMinutes, _options.MaxDurationMinutes);
        var expiresAt = DateTime.UtcNow.AddMinutes(duration);

        // 6. Create impersonation record
        var impersonationSession = new ImpersonationSession
        {
            ImpersonatorId = impersonatorUserId,
            ImpersonatorSessionKey = impersonatorSessionKey,
            TargetUserId = targetUserId,
            Reason = reason,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            StartedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };

        _context.ImpersonationSessions.Add(impersonationSession);
        await _context.SaveChangesAsync();

        // 7. Create new session with impersonation metadata
        var impersonatorInfo = new ImpersonatorInfo(
            impersonatorUserId,
            impersonator.Username,
            impersonatorSessionKey
        );

        var impersonatedSessionKey = await _sessionService.CreateImpersonatedSessionAsync(
            targetUser.Id,
            targetUser.Username,
            targetUser.Email,
            targetUser.IsSuperAdmin,
            impersonatorInfo,
            impersonationSession.Id,
            expiresAt,
            ipAddress,
            userAgent);

        // 8. Update impersonation record with new session key
        impersonationSession.ImpersonatedSessionKey = impersonatedSessionKey;
        await _context.SaveChangesAsync();

        _logger.LogWarning(
            "IMPERSONATION STARTED: Admin {AdminUsername} ({AdminId}) impersonating {TargetUsername} ({TargetId}). Reason: {Reason}. Expires: {ExpiresAt}",
            impersonator.Username, impersonatorUserId, targetUser.Username, targetUserId, reason, expiresAt);

        return new ImpersonationResult(true, null, impersonatedSessionKey, impersonationSession.Id, expiresAt);
    }

    public async Task<CanImpersonateResult> CanImpersonateAsync(Guid impersonatorId, Guid targetId)
    {
        // Self-impersonation check
        if (impersonatorId == targetId)
        {
            return new CanImpersonateResult(false, "Cannot impersonate yourself");
        }

        var impersonator = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == impersonatorId);

        var target = await _context.Users.FindAsync(targetId);

        if (impersonator == null)
        {
            return new CanImpersonateResult(false, "Impersonator not found");
        }

        if (target == null)
        {
            return new CanImpersonateResult(false, "Target user not found");
        }

        if (!target.IsActive)
        {
            return new CanImpersonateResult(false, "Cannot impersonate disabled user");
        }

        // Super Admin cannot be impersonated
        if (target.IsSuperAdmin)
        {
            return new CanImpersonateResult(false, "Cannot impersonate Super Admin");
        }

        // Check if target is already being impersonated
        var isBeingImpersonated = await _context.ImpersonationSessions
            .AnyAsync(s => s.TargetUserId == targetId && s.EndedAt == null);

        if (isBeingImpersonated)
        {
            return new CanImpersonateResult(false, "User is already being impersonated");
        }

        // Check if impersonator is currently being impersonated (prevent chaining)
        var impersonatorSession = await _context.ImpersonationSessions
            .FirstOrDefaultAsync(s => s.ImpersonatedSessionKey != null &&
                s.EndedAt == null &&
                s.TargetUserId == impersonatorId);

        if (impersonatorSession != null)
        {
            return new CanImpersonateResult(false, "Cannot impersonate while being impersonated");
        }

        // Super Admin can impersonate anyone (except other super admins)
        if (impersonator.IsSuperAdmin)
        {
            return new CanImpersonateResult(true, null);
        }

        // Check for impersonate permission
        var hasPermission = impersonator.UserRoles
            .Any(ur => ur.Role.PermissionsJson.Contains("\"impersonate\":true"));

        if (!hasPermission)
        {
            return new CanImpersonateResult(false, "You don't have impersonation permission");
        }

        // Non-super-admin cannot impersonate admins
        var targetIsAdmin = target.UserRoles?.Any(ur =>
            ur.Role.PermissionsJson.Contains("\"admin\":true")) ?? false;

        if (targetIsAdmin)
        {
            return new CanImpersonateResult(false, "Cannot impersonate admin users");
        }

        return new CanImpersonateResult(true, null);
    }

    public async Task<string?> EndAsync(string impersonatedSessionKey, string endReason)
    {
        // 1. Get session data
        var sessionData = await _sessionService.GetSessionAsync(impersonatedSessionKey);
        if (sessionData == null || !sessionData.IsImpersonated)
        {
            return null;
        }

        // 2. Get impersonation record
        var impersonationSession = await _context.ImpersonationSessions
            .FirstOrDefaultAsync(s => s.Id == sessionData.ImpersonationId && s.EndedAt == null);

        if (impersonationSession == null)
        {
            return null;
        }

        // 3. End impersonation record
        impersonationSession.EndedAt = DateTime.UtcNow;
        impersonationSession.EndReason = endReason;
        await _context.SaveChangesAsync();

        // 4. Delete impersonated session from Redis
        await _sessionService.InvalidateSessionAsync(impersonatedSessionKey);

        // 5. Return original session key (admin returns to their session)
        var originalSessionKey = sessionData.Impersonator?.OriginalSessionKey;

        _logger.LogWarning(
            "IMPERSONATION ENDED: Session {ImpersonationId} ended. Reason: {EndReason}",
            impersonationSession.Id, endReason);

        return originalSessionKey;
    }

    public async Task<ImpersonationStatusResponse?> GetStatusAsync(string sessionKey)
    {
        var sessionData = await _sessionService.GetSessionAsync(sessionKey);
        if (sessionData == null || !sessionData.IsImpersonated)
        {
            return new ImpersonationStatusResponse(false, null, null, null, null, null, null);
        }

        var impersonationSession = await _context.ImpersonationSessions
            .Include(s => s.Impersonator)
            .Include(s => s.TargetUser)
            .FirstOrDefaultAsync(s => s.Id == sessionData.ImpersonationId);

        if (impersonationSession == null || impersonationSession.EndedAt != null)
        {
            // Session ended, clean up
            return new ImpersonationStatusResponse(false, null, null, null, null, null, null);
        }

        var remainingMinutes = Math.Max(0, (int)(impersonationSession.ExpiresAt - DateTime.UtcNow).TotalMinutes);

        return new ImpersonationStatusResponse(
            true,
            impersonationSession.Id,
            impersonationSession.TargetUser.Username,
            impersonationSession.TargetUser.Email,
            impersonationSession.ExpiresAt,
            remainingMinutes,
            impersonationSession.Impersonator.Username
        );
    }

    public async Task<List<ImpersonationSessionDto>> GetActiveSessionsAsync()
    {
        var activeSessions = await _context.ImpersonationSessions
            .Include(s => s.Impersonator)
            .Include(s => s.TargetUser)
            .Where(s => s.EndedAt == null)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();

        return activeSessions.Select(s => new ImpersonationSessionDto(
            s.Id,
            s.Impersonator.Username,
            s.TargetUser.Username,
            s.Reason,
            s.StartedAt,
            s.ExpiresAt,
            Math.Max(0, (int)(s.ExpiresAt - DateTime.UtcNow).TotalMinutes),
            s.IpAddress
        )).ToList();
    }

    public async Task<bool> ForceEndAsync(Guid impersonationId, Guid adminId, string reason)
    {
        var session = await _context.ImpersonationSessions
            .Include(s => s.Impersonator)
            .Include(s => s.TargetUser)
            .FirstOrDefaultAsync(s => s.Id == impersonationId);

        if (session == null)
        {
            return false;
        }

        session.EndedAt = DateTime.UtcNow;
        session.EndReason = reason;
        await _context.SaveChangesAsync();

        // Invalidate the impersonated session
        if (!string.IsNullOrEmpty(session.ImpersonatedSessionKey))
        {
            await _sessionService.InvalidateSessionAsync(session.ImpersonatedSessionKey);
        }

        _logger.LogWarning(
            "IMPERSONATION FORCE ENDED: Admin {AdminId} force ended impersonation {ImpersonationId} ({Impersonator} -> {Target}). Reason: {Reason}",
            adminId, impersonationId, session.Impersonator.Username, session.TargetUser.Username, reason);

        return true;
    }

    public async Task CleanupExpiredSessionsAsync()
    {
        var expiredSessions = await _context.ImpersonationSessions
            .Where(s => s.EndedAt == null && s.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();

        foreach (var session in expiredSessions)
        {
            session.EndedAt = DateTime.UtcNow;
            session.EndReason = "expired";

            // Invalidate the impersonated session
            if (!string.IsNullOrEmpty(session.ImpersonatedSessionKey))
            {
                await _sessionService.InvalidateSessionAsync(session.ImpersonatedSessionKey);
            }
        }

        await _context.SaveChangesAsync();

        if (expiredSessions.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired impersonation sessions", expiredSessions.Count);
        }
    }
}
