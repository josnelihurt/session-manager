using SessionManager.Api.Models.Invitations;

namespace SessionManager.Api.Services;

public interface IEmailService
{
    Task<bool> SendInvitationEmailAsync(string toEmail, InvitationDto invitation, string[] applicationUrls);
}
