using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Models.Emails;
using SessionManager.Api.Models.Invitations;
using SessionManager.Api.Templates;
using StackExchange.Redis;

namespace SessionManager.Api.Services.Email;

public class EmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailService> _logger;
    private readonly IEmailTemplateService _templateService;
    private readonly IConnectionMultiplexer _redis;
    private const string EmailQueueKey = "email:queue";

    public EmailService(
        IOptions<EmailOptions> options,
        ILogger<EmailService> logger,
        IEmailTemplateService templateService,
        IConnectionMultiplexer redis)
    {
        _options = options.Value;
        _logger = logger;
        _templateService = templateService;
        _redis = redis;
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
        var body = _templateService.GenerateInvitationEmail(invitation, applicationUrls);
        return await SendEmailAsync(toEmail, subject, body);
    }

    public async Task EnqueueInvitationEmailAsync(InvitationDto invitation, string[] applicationUrls)
    {
        var subject = "You're invited to join josnelihurt.me Lab";
        var body = _templateService.GenerateInvitationEmail(invitation, applicationUrls);

        var emailJob = new EmailJob(
            invitation.Email,
            subject,
            body,
            DateTime.UtcNow
        );

        var json = System.Text.Json.JsonSerializer.Serialize(emailJob);
        var db = _redis.GetDatabase();
        await db.ListLeftPushAsync(EmailQueueKey, json);
    }
}
