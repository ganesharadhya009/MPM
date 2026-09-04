namespace PeopleHQ.Application.Common.Interfaces;

/// <summary>
/// Fire-and-forget notification dispatch used by any module (Workflow, Attendance, Leave, Timesheet,
/// Payroll, etc.) to notify an employee of an event, without that module needing to know about channels
/// or preferences. Honors per-employee, per-category NotificationPreference rows (FR-NOTIF-01): the in-app
/// channel defaults to enabled when no preference row exists, the email channel defaults to disabled
/// (opt-in) — see PeopleHQ.Infrastructure.Notifications.NotificationService for the concrete policy.
/// </summary>
public interface INotificationService
{
    Task NotifyAsync(Guid recipientEmployeeId, string category, string title, string body, string? link = null, CancellationToken ct = default);
}
