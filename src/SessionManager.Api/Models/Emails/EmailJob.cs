namespace SessionManager.Api.Models.Emails;

public record EmailJob(
    string ToEmail,
    string Subject,
    string HtmlBody,
    DateTime CreatedAt
);
