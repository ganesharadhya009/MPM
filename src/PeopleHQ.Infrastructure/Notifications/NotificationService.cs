using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Domain.Employees;
using PeopleHQ.Domain.Notifications;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Notifications;

/// <summary>
/// Concrete INotificationService (FR-NOTIF-01). Channel policy per category:
///  - InApp: enabled unless the employee has an explicit NotificationPreference row disabling it for this
///    category (opt-out — every employee sees in-app notifications by default).
///  - Email: disabled unless the employee has an explicit NotificationPreference row enabling it for this
///    category (opt-in — avoids surprise email volume until a tenant/employee turns it on).
/// </summary>
public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly ITenantContext _tenant;

    public NotificationService(AppDbContext db, IEmailSender emailSender, ITenantContext tenant)
    {
        _db = db;
        _emailSender = emailSender;
        _tenant = tenant;
    }

    public async Task NotifyAsync(Guid recipientEmployeeId, string category, string title, string body, string? link = null, CancellationToken ct = default)
    {
        var preferences = await _db.NotificationPreferences
            .Where(p => p.EmployeeId == recipientEmployeeId && p.Category == category)
            .ToListAsync(ct);

        var inAppEnabled = preferences.FirstOrDefault(p => p.Channel == NotificationChannel.InApp)?.Enabled ?? true;
        var emailEnabled = preferences.FirstOrDefault(p => p.Channel == NotificationChannel.Email)?.Enabled ?? false;

        if (inAppEnabled)
        {
            _db.Notifications.Add(new Notification
            {
                TenantId = _tenant.TenantId,
                RecipientEmployeeId = recipientEmployeeId,
                Category = category,
                Title = title,
                Body = body,
                Link = link,
                IsRead = false
            });
            await _db.SaveChangesAsync(ct);
        }

        if (emailEnabled)
        {
            var employee = await _db.Employees.FindAsync(new object[] { recipientEmployeeId }, ct);
            var toEmail = employee?.WorkEmail ?? employee?.PersonalEmail;
            if (!string.IsNullOrWhiteSpace(toEmail))
                await _emailSender.SendAsync(toEmail, title, body, ct);
        }
    }
}
