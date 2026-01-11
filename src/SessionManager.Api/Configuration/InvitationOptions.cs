namespace SessionManager.Api.Configuration;

public class InvitationOptions
{
    public const string SectionName = "Invitation";
    public int TokenLifetimeDays { get; set; } = 7;
}
