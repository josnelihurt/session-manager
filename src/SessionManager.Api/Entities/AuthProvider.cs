namespace SessionManager.Api.Entities;

public class AuthProvider
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string ConfigJson { get; set; } = "{}";
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
