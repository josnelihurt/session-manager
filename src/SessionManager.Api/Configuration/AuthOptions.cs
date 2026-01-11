namespace SessionManager.Api.Configuration;

public class AuthOptions
{
    public const string SectionName = "Auth";
    public string JwtSecret { get; set; } = string.Empty;
    public string JwtIssuer { get; set; } = "session-manager.lab.josnelihurt.me";
    public string JwtAudience { get; set; } = "lab.josnelihurt.me";
    public int SessionLifetimeHours { get; set; } = 4;
    public string CookieDomain { get; set; } = ".lab.josnelihurt.me";
    public string CookieName { get; set; } = "_session_manager";
}
