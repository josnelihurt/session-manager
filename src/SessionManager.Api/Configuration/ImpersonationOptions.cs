namespace SessionManager.Api.Configuration;

public class ImpersonationOptions
{
    public const string SectionName = "Impersonation";
    public int MaxDurationMinutes { get; set; } = 60;
    public bool Enabled { get; set; } = true;
}
