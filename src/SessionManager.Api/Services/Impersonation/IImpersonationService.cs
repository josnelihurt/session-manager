using SessionManager.Api.Models.Impersonation;

namespace SessionManager.Api.Services.Impersonation;

public interface IImpersonationService
{
    /// <summary>
    /// Start impersonating a user. Returns new session key.
    /// </summary>
    Task<ImpersonationResult> StartAsync(
        Guid impersonatorUserId,
        string impersonatorSessionKey,
        Guid targetUserId,
        string reason,
        int durationMinutes,
        string ipAddress,
        string userAgent);

    /// <summary>
    /// End current impersonation. Returns original session key.
    /// </summary>
    Task<string?> EndAsync(string impersonatedSessionKey, string endReason);

    /// <summary>
    /// Check if user can impersonate target
    /// </summary>
    Task<CanImpersonateResult> CanImpersonateAsync(Guid impersonatorId, Guid targetId);

    /// <summary>
    /// Get impersonation status from session
    /// </summary>
    Task<ImpersonationStatusResponse?> GetStatusAsync(string sessionKey);

    /// <summary>
    /// Get all active impersonation sessions
    /// </summary>
    Task<List<ImpersonationSessionDto>> GetActiveSessionsAsync();

    /// <summary>
    /// Force-end an impersonation session (admin action)
    /// </summary>
    Task<bool> ForceEndAsync(Guid impersonationId, Guid adminId, string reason);

    /// <summary>
    /// Check and cleanup expired impersonation sessions
    /// </summary>
    Task CleanupExpiredSessionsAsync();
}
