namespace SessionManager.Api.Configuration;

public class EmailOptions
{
    public const string SectionName = "Email";
    public string From { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
}
