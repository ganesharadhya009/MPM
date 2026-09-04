using MediatR;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.Workflow;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Workflow;

/// <summary>
/// Generic requester-facing notification for every workflow resolution, independent of the module-specific
/// INotificationHandler&lt;WorkflowRequestResolvedNotification&gt; implementations (Attendance/Leave/Timesheet/
/// Payroll) that apply their own domain side effects — MediatR dispatches this notification to all
/// registered handlers, so this one only ever notifies, never mutates module state.
/// </summary>
public class WorkflowNotificationHandler : INotificationHandler<WorkflowRequestResolvedNotification>
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notificationService;

    public WorkflowNotificationHandler(AppDbContext db, INotificationService notificationService)
    {
        _db = db;
        _notificationService = notificationService;
    }

    public async Task Handle(WorkflowRequestResolvedNotification notification, CancellationToken ct)
    {
        var request = await _db.WorkflowRequests.FindAsync(new object[] { notification.WorkflowRequestId }, ct);
        if (request is null) return;

        await _notificationService.NotifyAsync(request.RequesterEmployeeId, "workflow.resolution",
            $"Your {notification.RequestType} request was {notification.Status}",
            $"Your {notification.RequestType} request has been {notification.Status}.", ct: ct);
    }
}
