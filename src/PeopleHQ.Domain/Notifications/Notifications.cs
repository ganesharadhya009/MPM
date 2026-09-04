using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.Notifications;

public enum NotificationChannel { InApp, Email }

public class Notification : TenantOwnedEntity
{
    public Guid RecipientEmployeeId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Link { get; set; }
    public bool IsRead { get; set; }
}

public class NotificationPreference
{
    public Guid EmployeeId { get; set; }
    public string Category { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public bool Enabled { get; set; } = true;
}
