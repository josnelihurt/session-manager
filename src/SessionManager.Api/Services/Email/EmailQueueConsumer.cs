using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Models.Emails;
using StackExchange.Redis;

namespace SessionManager.Api.Services.Email;

public class EmailQueueConsumer : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailQueueConsumer> _logger;
    private readonly EmailOptions _emailOptions;
    private readonly string _queueKey = "email:queue";

    public EmailQueueConsumer(
        IConnectionMultiplexer redis,
        IEmailService emailService,
        ILogger<EmailQueueConsumer> logger,
        IOptions<EmailOptions> emailOptions)
    {
        _redis = redis;
        _emailService = emailService;
        _logger = logger;
        _emailOptions = emailOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email Queue Consumer started");

        var db = _redis.GetDatabase();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Blocking read from the right side of the list (FIFO)
                // Using BRPOPLPUSH for reliable queue processing
                var value = await db.ListRightPopLeftPushAsync(
                    _queueKey,
                    $"{_queueKey}:processing"
                );

                if (value.IsNullOrEmpty)
                {
                    // Queue is empty, wait a bit before retrying
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                var job = System.Text.Json.JsonSerializer.Deserialize<EmailJob>(value!);
                if (job == null)
                {
                    _logger.LogWarning("Failed to deserialize email job");
                    // Remove from processing queue
                    await db.ListRightPopAsync($"{_queueKey}:processing");
                    continue;
                }

                _logger.LogInformation("Processing email job for {Email}", job.ToEmail);

                var success = await SendEmailAsync(job, stoppingToken);

                if (success)
                {
                    // Remove from processing queue
                    await db.ListRightPopAsync($"{_queueKey}:processing");
                    _logger.LogInformation("Email sent successfully to {Email}", job.ToEmail);
                }
                else
                {
                    // Failed to send, move back to main queue for retry
                    await db.ListLeftPushAsync(_queueKey, value);
                    await db.ListRightPopAsync($"{_queueKey}:processing");
                    _logger.LogWarning("Failed to send email to {Email}, requeued", job.ToEmail);
                    // Wait before retry to avoid tight loop on persistent failures
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing email queue");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Email Queue Consumer stopped");
    }

    private async Task<bool> SendEmailAsync(EmailJob job, CancellationToken cancellationToken)
    {
        try
        {
            return await _emailService.SendEmailAsync(job.ToEmail, job.Subject, job.HtmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {Email}", job.ToEmail);
            return false;
        }
    }
}
