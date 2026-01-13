namespace SessionManager.Api.Entities;

public class OtpAttempt
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public bool IsUsed => UsedAt != null;
}
