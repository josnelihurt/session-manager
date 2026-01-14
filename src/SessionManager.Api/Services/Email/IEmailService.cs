using SessionManager.Api.Models.Invitations;

namespace SessionManager.Api.Services.Email;

public interface IEmailService
{
    Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);
    Task<bool> SendInvitationEmailAsync(string toEmail, InvitationDto invitation, string[] applicationUrls);
}
