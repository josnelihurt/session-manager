namespace SessionManager.Api.Configuration;

public class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";
    public string SessionKeyPattern { get; set; } = "*_oauth2_proxy_*";
    public int ScanPageSize { get; set; } = 100;
}

public class CorsOptions
{
    public const string SectionName = "Cors";

    public string FrontendUrl { get; set; } = "https://session-manager.lab.josnelihurt.me";
    public string[] AdditionalOrigins { get; set; } = [];
}
