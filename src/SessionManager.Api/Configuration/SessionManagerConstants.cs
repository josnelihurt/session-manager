namespace SessionManager.Api.Configuration;

public static class SessionManagerConstants
{
    // ============================================================
    // REDIS
    // ============================================================
    public const string RedisSessionPrefix = "session_manager:";
    public const string OAuth2ProxyRedisPattern = "_oauth2_proxy_redis:*";

    // ============================================================
    // PROVIDERS
    // ============================================================
    public const string LocalProvider = "local";
    public const string GoogleProvider = "google";
    public const string Auth0Provider = "auth0";

    // Provider Display Names
    public const string LocalProviderDisplayName = "Email/Password";
    public const string GoogleProviderDisplayName = "Google";
    public const string Auth0ProviderDisplayName = "Auth0";

    // ============================================================
    // ROLES
    // ============================================================
    public const string AdminRole = "admin";
    public const string UserRole = "user";
    public const string ViewerRole = "viewer";

    // ============================================================
    // PERMISSIONS (Keys for Dictionary)
    // ============================================================
    public const string PermissionRead = "read";
    public const string PermissionWrite = "write";
    public const string PermissionDelete = "delete";
    public const string PermissionAdmin = "admin";

    // ============================================================
    // COOKIES
    // ============================================================
    public const string SessionCookieName = "_session_manager";
    public const string CookieDomain = ".lab.josnelihurt.me";

    // ============================================================
    // HTTP HEADERS
    // ============================================================
    public static class HttpHeaders
    {
        // Request Headers (from Traefik)
        public const string XForwardedHost = "X-Forwarded-Host";
        public const string XOriginalHost = "X-Original-Host";
        public const string XForwardedUri = "X-Forwarded-Uri";
        public const string XOriginalUri = "X-Original-URI";
        public const string Host = "Host";

        // Response Headers (to downstream services)
        public const string XAuthRequestUser = "X-Auth-Request-User";
        public const string XAuthRequestEmail = "X-Auth-Request-Email";
        public const string XSessionManagerId = "X-Session-Manager-Id";
        public const string XUserIsAdmin = "X-User-Is-Admin";

        // Authorization
        public const string Authorization = "Authorization";
        public const string BearerScheme = "Bearer";
    }

    // ============================================================
    // ROUTES
    // ============================================================
    public static class Routes
    {
        public const string ApiAuth = "/api/auth/";
        public const string ApiTest = "/api/test";
        public const string ApiStatic = "/api/static/";

        public const string Auth = "/auth";
        public const string AuthForward = "/auth-forward";

        // Frontend Routes
        public const string Login = "/login";
        public const string Register = "/register";
        public const string Dashboard = "/dashboard";
    }

    // ============================================================
    // URLS
    // ============================================================
    public static class Urls
    {
        public const string BaseUrl = "https://session-manager.lab.josnelihurt.me";
        public const string DashboardUrl = "https://session-manager.lab.josnelihurt.me/dashboard";

        // JWT
        public const string JwtIssuer = "session-manager.lab.josnelihurt.me";
        public const string JwtAudience = "lab.josnelihurt.me";

        // Google OAuth
        public const string GoogleAuthUrl = "https://accounts.google.com/o/oauth2/v2/auth";
        public const string GoogleTokenUrl = "https://oauth2.googleapis.com/token";
        public const string GoogleUserInfoUrl = "https://www.googleapis.com/oauth2/v2/userinfo";
    }
}
