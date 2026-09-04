using MediatR;
using PeopleHQ.Domain.Notifications;

namespace PeopleHQ.Application.Notifications;

// All queries/commands here are implicitly self-scoped to the caller's own employee id (resolved server-side
// via ICurrentEmployeeResolver) — no EmployeeId parameter is ever accepted from the client, so there is no
// IDOR surface to guard in this module.

public record GetMyNotificationsQuery(bool UnreadOnly = false, int Page = 1, int PageSize = 25) : IRequest<IReadOnlyList<NotificationDto>>;
public record NotificationDto(Guid Id, string Category, string Title, string Body, string? Link, bool IsRead, DateTime CreatedAtUtc);

public record GetUnreadNotificationCountQuery : IRequest<int>;

public record MarkNotificationReadCommand(Guid Id) : IRequest;
public record MarkAllNotificationsReadCommand : IRequest;

public record GetNotificationPreferencesQuery : IRequest<IReadOnlyList<NotificationPreferenceDto>>;
public record NotificationPreferenceDto(string Category, NotificationChannel Channel, bool Enabled);
public record UpdateNotificationPreferenceCommand(string Category, NotificationChannel Channel, bool Enabled) : IRequest;
