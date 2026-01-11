namespace SessionManager.Api.Models.Applications;

public record CreateApplicationRequest(
    string Url,
    string Name,
    string? Description
);

public record CreateRoleRequest(
    string Name,
    Dictionary<string, bool>? Permissions = null
);
