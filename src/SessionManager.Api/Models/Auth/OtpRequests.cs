namespace SessionManager.Api.Models.Auth;

public record VerifyOtpRequest(
    string Email,
    string Code
);

public record OtpRequiredResponse(
    string Message,
    string Email
);

public record LoginWithOtpRequest(
    string Username,
    string Password,
    string? OtpCode
);

public record LoginOtpResponse(
    bool RequiresOtp,
    string? Message,
    string? Email,
    UserInfo? User
);
