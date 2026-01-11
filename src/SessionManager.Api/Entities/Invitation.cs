namespace SessionManager.Api.Entities;

public class Invitation
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Provider { get; set; } = "any";
    public Guid[]? PreAssignedRoles { get; set; }
    public Guid CreatedById { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public Guid? UsedById { get; set; }
    public DateTime CreatedAt { get; set; }

    public User CreatedBy { get; set; } = null!;
    public User? UsedBy { get; set; }
}
