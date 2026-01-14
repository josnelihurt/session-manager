using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Models.Invitations;
using System.Text;

namespace SessionManager.Api.Services.Email;

public class EmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<EmailOptions> options,
        ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Session Manager", _options.From));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;

            message.Body = new TextPart("html")
            {
                Text = htmlBody
            };

            using var client = new SmtpClient();
            try
            {
                await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, MailKit.Security.SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_options.From, _options.Password);
                await client.SendAsync(message);
                _logger.LogInformation("Email sent successfully to {Email}", toEmail);
                return true;
            }
            finally
            {
                await client.DisconnectAsync(true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendInvitationEmailAsync(string toEmail, InvitationDto invitation, string[] applicationUrls)
    {
        var subject = "You're invited to join josnelihurt.me Lab";
        var body = GenerateInvitationEmailBody(invitation, applicationUrls);
        return await SendEmailAsync(toEmail, subject, body);
    }

    private string GenerateInvitationEmailBody(InvitationDto invitation, string[] applicationUrls)
    {
        var providerText = invitation.Provider switch
        {
            "google" => "Google",
            "local" => "Email/Password",
            _ => invitation.Provider
        };

        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("    <title>Invitation to josnelihurt.me Lab</title>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body style=\"margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #0a0e27; color: #f2f2f2;\">");
        sb.AppendLine("    <div style=\"max-width: 600px; margin: 0 auto; background: linear-gradient(135deg, #0a0e27 0%, #1a1a3e 100%);\">");
        sb.AppendLine("        <!-- Header -->");
        sb.AppendLine("        <div style=\"padding: 40px 20px; text-align: center; border-bottom: 2px solid rgba(244, 211, 94, 0.3);\">");
        sb.AppendLine("            <h1 style=\"margin: 0; color: #F4D35E; font-size: 28px; font-weight: 600;\">You're Invited!</h1>");
        sb.AppendLine("            <p style=\"margin: 10px 0 0; color: rgba(242, 242, 242, 0.8); font-size: 16px;\">Join the josnelihurt.me Lab</p>");
        sb.AppendLine("        </div>");

        sb.AppendLine("        <!-- Main Content -->");
        sb.AppendLine("        <div style=\"padding: 40px 30px;\">");
        sb.AppendLine("            <!-- Greeting -->");
        sb.AppendLine("            <p style=\"margin: 0 0 20px; color: #f2f2f2; font-size: 16px; line-height: 1.6;\">");
        sb.AppendLine("                Hello!");
        sb.AppendLine("            </p>");
        sb.AppendLine("            <p style=\"margin: 0 0 30px; color: rgba(242, 242, 242, 0.9); font-size: 16px; line-height: 1.6;\">");
        sb.AppendLine("                You have been invited to access the <strong style=\"color: #F4D35E;\">josnelihurt.me Lab</strong> ");
        sb.AppendLine($"                using <strong style=\"color: #F4D35E;\">{providerText}</strong> authentication.");
        sb.AppendLine("            </p>");

        // Application URLs section
        if (applicationUrls != null && applicationUrls.Length > 0)
        {
            sb.AppendLine("            <!-- Applications -->");
            sb.AppendLine("            <div style=\"margin: 30px 0; padding: 25px; background: rgba(244, 211, 94, 0.1); border: 1px solid rgba(244, 211, 94, 0.3); border-radius: 8px;\">");
            sb.AppendLine("                <h2 style=\"margin: 0 0 15px; color: #F4D35E; font-size: 18px;\">Your Applications</h2>");
            sb.AppendLine("                <p style=\"margin: 0 0 20px; color: rgba(242, 242, 242, 0.8); font-size: 14px;\">");
            sb.AppendLine("                    You have been granted access to the following applications:");
            sb.AppendLine("                </p>");
            sb.AppendLine("                <ul style=\"margin: 0; padding-left: 20px; color: #f2f2f2; font-size: 14px; line-height: 1.8;\">");

            foreach (var appUrl in applicationUrls)
            {
                sb.AppendLine($"                    <li style=\"margin-bottom: 8px;\"><a href=\"https://{appUrl}\" style=\"color: #F4D35E; text-decoration: none; transition: color 0.3s;\">{appUrl}</a></li>");
            }

            sb.AppendLine("                </ul>");
            sb.AppendLine("            </div>");
        }

        sb.AppendLine("            <!-- CTA Button -->");
        sb.AppendLine("            <div style=\"text-align: center; margin: 40px 0;\">");
        sb.AppendLine($"                <a href=\"{invitation.InviteUrl}\" style=\"display: inline-block; padding: 15px 40px; background: linear-gradient(135deg, #F4D35E 0%, #F2A900 100%); color: #1a1a1a; text-decoration: none; border-radius: 8px; font-weight: 600; font-size: 16px; box-shadow: 0 4px 15px rgba(244, 211, 94, 0.3); transition: all 0.3s;\">");
        sb.AppendLine("                    Create Your Account");
        sb.AppendLine("                </a>");
        sb.AppendLine("            </div>");

        sb.AppendLine("            <!-- Invitation Details -->");
        sb.AppendLine("            <div style=\"margin: 30px 0; padding: 20px; background: rgba(255, 255, 255, 0.05); border-radius: 8px; border: 1px solid rgba(255, 255, 255, 0.1);\">");
        sb.AppendLine("                <p style=\"margin: 0 0 10px; color: rgba(242, 242, 242, 0.7); font-size: 13px;\">");
        sb.AppendLine($"                    <strong>Invitation Type:</strong> {providerText}");
        sb.AppendLine("                </p>");
        sb.AppendLine("                <p style=\"margin: 5px 0 0; color: rgba(242, 242, 242, 0.7); font-size: 13px;\">");
        sb.AppendLine($"                    <strong>Expires:</strong> {invitation.ExpiresAt:MMM dd, yyyy}");
        sb.AppendLine("                </p>");
        sb.AppendLine("            </div>");

        sb.AppendLine("            <!-- Spiderman Quote -->");
        sb.AppendLine("            <div style=\"margin: 40px 0; padding: 25px; text-align: center; border-top: 1px solid rgba(244, 211, 94, 0.2); border-bottom: 1px solid rgba(244, 211, 94, 0.2);\">");
        sb.AppendLine("                <p style=\"margin: 0; color: #F4D35E; font-size: 18px; font-style: italic; font-weight: 500;\">");
        sb.AppendLine("                    \"With great power comes great responsibility.\"");
        sb.AppendLine("                </p>");
        sb.AppendLine("                <p style=\"margin: 10px 0 0; color: rgba(242, 242, 242, 0.6); font-size: 12px;\">");
        sb.AppendLine("                    - Uncle Ben, Spider-Man");
        sb.AppendLine("                </p>");
        sb.AppendLine("            </div>");

        sb.AppendLine("            <!-- Dashboard Link -->");
        sb.AppendLine("            <p style=\"margin: 30px 0 0; text-align: center; color: rgba(242, 242, 242, 0.7); font-size: 14px;\">");
        sb.AppendLine("                After creating your account, you can access the dashboard at:<br>");
        sb.AppendLine($"                <a href=\"{SessionManagerConstants.Urls.DashboardUrl}\" style=\"color: #F4D35E; text-decoration: none; font-weight: 500;\">session-manager.lab.josnelihurt.me/dashboard</a>");
        sb.AppendLine("            </p>");
        sb.AppendLine("        </div>");

        sb.AppendLine("        <!-- Footer -->");
        sb.AppendLine("        <div style=\"padding: 30px; text-align: center; border-top: 1px solid rgba(255, 255, 255, 0.1); background: rgba(10, 14, 39, 0.5);\">");
        sb.AppendLine("            <p style=\"margin: 0; color: rgba(242, 242, 242, 0.5); font-size: 12px;\">");
        sb.AppendLine("                This is an automated message from Session Manager. Please do not reply directly to this email.");
        sb.AppendLine("            </p>");
        sb.AppendLine("        </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
