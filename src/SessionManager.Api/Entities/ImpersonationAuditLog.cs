namespace SessionManager.Api.Entities;

public class ImpersonationAuditLog
{
    public Guid Id { get; set; }
    public Guid ImpersonationSessionId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string? HttpMethod { get; set; }
    public string? Endpoint { get; set; }
    public string? RequestBodyHash { get; set; }
    public int? ResponseStatusCode { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation property
    public ImpersonationSession ImpersonationSession { get; set; } = null!;
}
