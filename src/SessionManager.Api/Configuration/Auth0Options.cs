namespace SessionManager.Api.Configuration;

public class Auth0Options
{
    public const string SectionName = "Auth0";

    public string Domain { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;

    // Management API Configuration
    public string ManagementApiClientId { get; set; } = string.Empty;
    public string ManagementApiClientSecret { get; set; } = string.Empty;
    public string ManagementApiIdentifier { get; set; } = string.Empty;
}
