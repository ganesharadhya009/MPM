using Microsoft.Extensions.Logging;
using PeopleHQ.Application.Common.Interfaces;

namespace PeopleHQ.Infrastructure.Notifications;

/// <summary>
/// Placeholder IEmailSender: logs the send instead of dispatching through a real transport. Wiring an
/// actual provider (SMTP/SendGrid/SES) is a documented follow-up — out of scope for the current backend
/// pass, same v1-simplification treatment as the payslip PDF placeholder in PayrollRunHandlers.
/// </summary>
public class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;
    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        _logger.LogInformation("Email (placeholder transport) to {ToEmail}: {Subject}", toEmail, subject);
        return Task.CompletedTask;
    }
}
