namespace PeopleHQ.Application.Common.Interfaces;

/// <summary>Abstraction over the outbound email transport, so the concrete provider (SMTP, SendGrid, SES,
/// etc.) can be swapped in Infrastructure without touching callers. See NotificationService for the
/// placeholder v1 implementation.</summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default);
}
