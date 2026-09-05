using MediatR;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.Workflow;
using PeopleHQ.Domain.Integrations;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Integrations;

/// <summary>
/// Fires the WorkflowRequestResolved webhook event for every tenant subscription — another
/// INotificationHandler&lt;WorkflowRequestResolvedNotification&gt; alongside the existing Attendance/Leave/
/// Timesheet/Payroll/generic-notification handlers, following the same fan-out pattern. This is the only
/// webhook event wired up in this pass (covers Leave/Regularization/Timesheet/PayrollRun/HR-process
/// resolutions); Employee-created/-exited and other lifecycle events are a documented follow-up.
/// </summary>
public class WebhookNotificationHandler : INotificationHandler<WorkflowRequestResolvedNotification>
{
    private readonly AppDbContext _db;
    private readonly IWebhookDispatcher _dispatcher;
    public WebhookNotificationHandler(AppDbContext db, IWebhookDispatcher dispatcher) { _db = db; _dispatcher = dispatcher; }

    public async Task Handle(WorkflowRequestResolvedNotification notification, CancellationToken ct)
    {
        var request = await _db.WorkflowRequests.FindAsync(new object[] { notification.WorkflowRequestId }, ct);
        if (request is null) return;

        await _dispatcher.DispatchAsync(request.TenantId, WebhookEventType.WorkflowRequestResolved, new
        {
            workflowRequestId = notification.WorkflowRequestId,
            requestType = notification.RequestType.ToString(),
            status = notification.Status.ToString(),
            requesterEmployeeId = request.RequesterEmployeeId
        }, ct);
    }
}
