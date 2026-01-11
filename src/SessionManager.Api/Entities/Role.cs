namespace SessionManager.Api.Entities;

public class Role
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PermissionsJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }

    public Application Application { get; set; } = null!;
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
