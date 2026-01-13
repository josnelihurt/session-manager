using Microsoft.EntityFrameworkCore;
using SessionManager.Api.Data;
using SessionManager.Api.Models.Emails;
using StackExchange.Redis;

namespace SessionManager.Api.Services;

public class OtpService : IOtpService
{
    private readonly SessionManagerDbContext _dbContext;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<OtpService> _logger;
    private const string EmailQueueKey = "email:queue";
    private const int OtpExpirationMinutes = 10;
    private const int OtpCodeLength = 6;

    public OtpService(
        SessionManagerDbContext dbContext,
        IConnectionMultiplexer redis,
        ILogger<OtpService> logger)
    {
        _dbContext = dbContext;
        _redis = redis;
        _logger = logger;
    }

    public async Task<string> GenerateOtpAsync(string email)
    {
        // Invalidate any existing unused OTPs for this email
        var existingOtps = await _dbContext.OtpAttempts
            .Where(o => o.Email == email.ToLowerInvariant() && o.UsedAt == null && o.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var otp in existingOtps)
        {
            otp.UsedAt = DateTime.UtcNow; // Mark as used to invalidate
        }

        // Generate random 6-digit code
        var random = new Random();
        var code = random.Next(100000, 999999).ToString();

        var otpAttempt = new Entities.OtpAttempt
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            Code = code,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpirationMinutes)
        };

        _dbContext.OtpAttempts.Add(otpAttempt);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Generated OTP for {Email}, expires at {ExpiresAt}", email, otpAttempt.ExpiresAt);

        // Enqueue OTP email
        await EnqueueOtpEmailAsync(email, code, otpAttempt.ExpiresAt);

        return code; // Return for testing purposes (in production, don't return the code)
    }

    public async Task<bool> ValidateOtpAsync(string email, string code)
    {
        var otp = await _dbContext.OtpAttempts
            .Where(o => o.Email == email.ToLowerInvariant()
                      && o.Code == code
                      && o.UsedAt == null
                      && o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        return otp != null;
    }

    public async Task<bool> VerifyAndConsumeOtpAsync(string email, string code)
    {
        var otp = await _dbContext.OtpAttempts
            .Where(o => o.Email == email.ToLowerInvariant()
                      && o.Code == code
                      && o.UsedAt == null
                      && o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp == null)
        {
            _logger.LogWarning("Invalid or expired OTP for {Email}", email);
            return false;
        }

        // Mark as used
        otp.UsedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("OTP verified and consumed for {Email}", email);
        return true;
    }

    private async Task EnqueueOtpEmailAsync(string email, string code, DateTime expiresAt)
    {
        var subject = "Your Verification Code";
        var body = GenerateOtpEmailBody(code, expiresAt);

        var emailJob = new EmailJob(
            email,
            subject,
            body,
            DateTime.UtcNow
        );

        var json = System.Text.Json.JsonSerializer.Serialize(emailJob);
        var db = _redis.GetDatabase();
        await db.ListLeftPushAsync(EmailQueueKey, json);

        _logger.LogInformation("OTP email queued for {Email}", email);
    }

    private string GenerateOtpEmailBody(string code, DateTime expiresAt)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("    <title>Verification Code</title>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body style=\"margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #0a0e27; color: #f2f2f2;\">");
        sb.AppendLine("    <div style=\"max-width: 600px; margin: 0 auto; background: linear-gradient(135deg, #0a0e27 0%, #1a1a3e 100%);\">");
        sb.AppendLine("        <div style=\"padding: 40px 20px; text-align: center; border-bottom: 2px solid rgba(244, 211, 94, 0.3);\">");
        sb.AppendLine("            <h1 style=\"margin: 0; color: #F4D35E; font-size: 28px; font-weight: 600;\">Verification Code</h1>");
        sb.AppendLine("            <p style=\"margin: 10px 0 0; color: rgba(242, 242, 242, 0.8); font-size: 16px;\">Complete your login</p>");
        sb.AppendLine("        </div>");

        sb.AppendLine("        <div style=\"padding: 40px 30px;\">");
        sb.AppendLine("            <p style=\"margin: 0 0 20px; color: #f2f2f2; font-size: 16px; line-height: 1.6;\">");
        sb.AppendLine("                Use the following verification code to complete your login:");
        sb.AppendLine("            </p>");

        sb.AppendLine("            <div style=\"margin: 40px 0; padding: 30px; text-align: center; background: rgba(244, 211, 94, 0.1); border: 2px solid rgba(244, 211, 94, 0.3); border-radius: 12px;\">");
        sb.AppendLine("                <p style=\"margin: 0; color: #F4D35E; font-size: 48px; font-weight: 700; letter-spacing: 8px; font-family: 'Courier New', monospace;\">");
        sb.AppendLine($"                    {code}");
        sb.AppendLine("                </p>");
        sb.AppendLine("            </div>");

        sb.AppendLine("            <p style=\"margin: 30px 0 10px; color: rgba(242, 242, 242, 0.7); font-size: 14px;\">");
        sb.AppendLine("                This code will expire in 10 minutes.");
        sb.AppendLine("            </p>");
        sb.AppendLine("            <p style=\"margin: 5px 0 0; color: rgba(242, 242, 242, 0.7); font-size: 14px;\">");
        sb.AppendLine("                If you didn't request this code, please ignore this email.");
        sb.AppendLine("            </p>");

        sb.AppendLine("            <div style=\"margin: 40px 0; padding: 25px; text-align: center; border-top: 1px solid rgba(244, 211, 94, 0.2); border-bottom: 1px solid rgba(244, 211, 94, 0.2);\">");
        sb.AppendLine("                <p style=\"margin: 0; color: #F4D35E; font-size: 16px; font-weight: 500;\">");
        sb.AppendLine("                    Stay secure!");
        sb.AppendLine("                </p>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");

        sb.AppendLine("        <div style=\"padding: 30px; text-align: center; border-top: 1px solid rgba(255, 255, 255, 0.1); background: rgba(10, 14, 39, 0.5);\">");
        sb.AppendLine("            <p style=\"margin: 0; color: rgba(242, 242, 242, 0.5); font-size: 12px;\">");
        sb.AppendLine("                This is an automated message from Session Manager.");
        sb.AppendLine("            </p>");
        sb.AppendLine("        </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
