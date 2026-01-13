namespace SessionManager.Api.Services;

public interface IOtpService
{
    Task<string> GenerateOtpAsync(string email);
    Task<bool> ValidateOtpAsync(string email, string code);
    Task<bool> VerifyAndConsumeOtpAsync(string email, string code);
}
