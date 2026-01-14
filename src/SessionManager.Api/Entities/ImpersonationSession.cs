namespace SessionManager.Api.Entities;

public class ImpersonationSession
{
    public Guid Id { get; set; }
    public Guid ImpersonatorId { get; set; }
    public string ImpersonatorSessionKey { get; set; } = string.Empty;
    public Guid TargetUserId { get; set; }
    public string ImpersonatedSessionKey { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? EndReason { get; set; }

    // Navigation properties
    public User Impersonator { get; set; } = null!;
    public User TargetUser { get; set; } = null!;
    public ICollection<ImpersonationAuditLog> AuditLogs { get; set; } = new List<ImpersonationAuditLog>();
}
