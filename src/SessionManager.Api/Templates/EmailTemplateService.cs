using SessionManager.Api.Models.Invitations;
using SessionManager.Api.Configuration;

namespace SessionManager.Api.Templates;

public interface IEmailTemplateService
{
    string GenerateInvitationEmail(InvitationDto invitation, string[] applicationUrls);
}

public class EmailTemplateService : IEmailTemplateService
{
    public string GenerateInvitationEmail(InvitationDto invitation, string[] applicationUrls)
    {
        var providerText = invitation.Provider switch
        {
            SessionManagerConstants.GoogleProvider => SessionManagerConstants.GoogleProviderDisplayName,
            SessionManagerConstants.LocalProvider => SessionManagerConstants.LocalProviderDisplayName,
            _ => invitation.Provider
        };

        return $"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Invitation to josnelihurt.me Lab</title>
</head>
<body style="margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #0a0e27; color: #f2f2f2;">
    <div style="max-width: 600px; margin: 0 auto; background: linear-gradient(135deg, #0a0e27 0%, #1a1a3e 100%);">
        <div style="padding: 40px 20px; text-align: center; border-bottom: 2px solid rgba(244, 211, 94, 0.3);">
            <h1 style="margin: 0; color: #F4D35E; font-size: 28px; font-weight: 600;">You're Invited!</h1>
            <p style="margin: 10px 0 0; color: rgba(242, 242, 242, 0.8); font-size: 16px;">Join the josnelihurt.me Lab</p>
        </div>

        <div style="padding: 40px 30px;">
            <p style="margin: 0 0 20px; color: #f2f2f2; font-size: 16px; line-height: 1.6;">Hello!</p>
            <p style="margin: 0 0 30px; color: rgba(242, 242, 242, 0.9); font-size: 16px; line-height: 1.6;">
                You have been invited to access the <strong style="color: #F4D35E;">josnelihurt.me Lab</strong>
                using <strong style="color: #F4D35E;">{providerText}</strong> authentication.
            </p>

{GenerateApplicationsSection(applicationUrls)}

            <div style="text-align: center; margin: 40px 0;">
                <a href="{invitation.InviteUrl}" style="display: inline-block; padding: 15px 40px; background: linear-gradient(135deg, #F4D35E 0%, #F2A900 100%); color: #1a1a1a; text-decoration: none; border-radius: 8px; font-weight: 600; font-size: 16px;">
                    Create Your Account
                </a>
            </div>

            <div style="margin: 30px 0; padding: 20px; background: rgba(255, 255, 255, 0.05); border-radius: 8px; border: 1px solid rgba(255, 255, 255, 0.1);">
                <p style="margin: 0 0 10px; color: rgba(242, 242, 242, 0.7); font-size: 13px;">
                    <strong>Invitation Type:</strong> {providerText}
                </p>
                <p style="margin: 5px 0 0; color: rgba(242, 242, 242, 0.7); font-size: 13px;">
                    <strong>Expires:</strong> {invitation.ExpiresAt:MMM dd, yyyy}
                </p>
            </div>

            <div style="margin: 40px 0; padding: 25px; text-align: center; border-top: 1px solid rgba(244, 211, 94, 0.2); border-bottom: 1px solid rgba(244, 211, 94, 0.2);">
                <p style="margin: 0; color: #F4D35E; font-size: 18px; font-style: italic; font-weight: 500;">
                    "With great power comes great responsibility."
                </p>
                <p style="margin: 10px 0 0; color: rgba(242, 242, 242, 0.6); font-size: 12px;">
                    - Uncle Ben, Spider-Man
                </p>
            </div>

            <p style="margin: 30px 0 0; text-align: center; color: rgba(242, 242, 242, 0.7); font-size: 14px;">
                After creating your account, you can access the dashboard at:<br>
                <a href="{SessionManagerConstants.Urls.DashboardUrl}" style="color: #F4D35E; text-decoration: none; font-weight: 500;">session-manager.lab.josnelihurt.me/dashboard</a>
            </p>
        </div>

        <div style="padding: 30px; text-align: center; border-top: 1px solid rgba(255, 255, 255, 0.1); background: rgba(10, 14, 39, 0.5);">
            <p style="margin: 0; color: rgba(242, 242, 242, 0.5); font-size: 12px;">
                This is an automated message from Session Manager.
            </p>
        </div>
    </div>
</body>
</html>
""";
    }

    private string GenerateApplicationsSection(string[] urls)
    {
        if (urls == null || urls.Length == 0) return "";

        var items = string.Join("\n", urls.Select(url =>
            $"                    <li style=\"margin-bottom: 8px;\"><a href=\"https://{url}\" style=\"color: #F4D35E; text-decoration: none;\">{url}</a></li>"));

        return $"""
            <div style="margin: 30px 0; padding: 25px; background: rgba(244, 211, 94, 0.1); border: 1px solid rgba(244, 211, 94, 0.3); border-radius: 8px;">
                <h2 style="margin: 0 0 15px; color: #F4D35E; font-size: 18px;">Your Applications</h2>
                <p style="margin: 0 0 20px; color: rgba(242, 242, 242, 0.8); font-size: 14px;">You have been granted access to the following applications:</p>
                <ul style="margin: 0; padding-left: 20px; color: #f2f2f2; font-size: 14px; line-height: 1.8;">
{items}
                </ul>
            </div>
""";
    }
}
