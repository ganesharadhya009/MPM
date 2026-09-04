using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.Notifications;
using PeopleHQ.Domain.Notifications;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Notifications;

public class GetMyNotificationsQueryHandler : IRequestHandler<GetMyNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public GetMyNotificationsQueryHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task<IReadOnlyList<NotificationDto>> Handle(GetMyNotificationsQuery request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var query = _db.Notifications.Where(n => n.RecipientEmployeeId == employeeId);
        if (request.UnreadOnly) query = query.Where(n => !n.IsRead);

        return await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => new NotificationDto(n.Id, n.Category, n.Title, n.Body, n.Link, n.IsRead, n.CreatedAtUtc))
            .ToListAsync(ct);
    }
}

public class GetUnreadNotificationCountQueryHandler : IRequestHandler<GetUnreadNotificationCountQuery, int>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public GetUnreadNotificationCountQueryHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task<int> Handle(GetUnreadNotificationCountQuery request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        return await _db.Notifications.CountAsync(n => n.RecipientEmployeeId == employeeId && !n.IsRead, ct);
    }
}

public class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public MarkNotificationReadCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var notification = await _db.Notifications.FindAsync(new object[] { request.Id }, ct)
            ?? throw new NotFoundException(nameof(Notification), request.Id);
        if (notification.RecipientEmployeeId != employeeId) throw new ForbiddenException("You can only mark your own notifications as read.");

        notification.IsRead = true;
        await _db.SaveChangesAsync(ct);
    }
}

public class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public MarkAllNotificationsReadCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task Handle(MarkAllNotificationsReadCommand request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var unread = await _db.Notifications.Where(n => n.RecipientEmployeeId == employeeId && !n.IsRead).ToListAsync(ct);
        foreach (var notification in unread) notification.IsRead = true;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetNotificationPreferencesQueryHandler : IRequestHandler<GetNotificationPreferencesQuery, IReadOnlyList<NotificationPreferenceDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public GetNotificationPreferencesQueryHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task<IReadOnlyList<NotificationPreferenceDto>> Handle(GetNotificationPreferencesQuery request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        return await _db.NotificationPreferences
            .Where(p => p.EmployeeId == employeeId)
            .Select(p => new NotificationPreferenceDto(p.Category, p.Channel, p.Enabled))
            .ToListAsync(ct);
    }
}

public class UpdateNotificationPreferenceCommandHandler : IRequestHandler<UpdateNotificationPreferenceCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public UpdateNotificationPreferenceCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task Handle(UpdateNotificationPreferenceCommand request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var preference = await _db.NotificationPreferences.FindAsync(new object[] { employeeId, request.Category, request.Channel }, ct);
        if (preference is null)
        {
            _db.NotificationPreferences.Add(new NotificationPreference
            {
                EmployeeId = employeeId,
                Category = request.Category,
                Channel = request.Channel,
                Enabled = request.Enabled
            });
        }
        else
        {
            preference.Enabled = request.Enabled;
        }
        await _db.SaveChangesAsync(ct);
    }
}
