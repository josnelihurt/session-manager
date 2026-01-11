namespace SessionManager.Api.Entities;

public class Session
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string SessionKey { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public User User { get; set; } = null!;
}
