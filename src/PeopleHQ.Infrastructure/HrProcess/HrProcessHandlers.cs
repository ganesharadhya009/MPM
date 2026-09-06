using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.HrProcess;
using PeopleHQ.Application.Payroll;
using PeopleHQ.Application.Workflow;
using PeopleHQ.Domain.Employees;
using PeopleHQ.Domain.Workflow;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.HrProcess;

public class SubmitDepartmentChangeRequestCommandHandler : IRequestHandler<SubmitDepartmentChangeRequestCommand, Guid>
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public SubmitDepartmentChangeRequestCommandHandler(IWorkflowEngine workflowEngine, ICurrentEmployeeResolver employeeResolver) { _workflowEngine = workflowEngine; _employeeResolver = employeeResolver; }

    public async Task<Guid> Handle(SubmitDepartmentChangeRequestCommand request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var payload = new DepartmentChangePayload(request.NewDepartmentId, request.Reason);
        return await _workflowEngine.SubmitAsync(WorkflowRequestType.DepartmentChange, employeeId, payload, ct);
    }
}

public class SubmitLocationChangeRequestCommandHandler : IRequestHandler<SubmitLocationChangeRequestCommand, Guid>
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public SubmitLocationChangeRequestCommandHandler(IWorkflowEngine workflowEngine, ICurrentEmployeeResolver employeeResolver) { _workflowEngine = workflowEngine; _employeeResolver = employeeResolver; }

    public async Task<Guid> Handle(SubmitLocationChangeRequestCommand request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var payload = new LocationChangePayload(request.NewLocationId, request.Reason);
        return await _workflowEngine.SubmitAsync(WorkflowRequestType.LocationChange, employeeId, payload, ct);
    }
}

public class SubmitDesignationChangeRequestCommandHandler : IRequestHandler<SubmitDesignationChangeRequestCommand, Guid>
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public SubmitDesignationChangeRequestCommandHandler(IWorkflowEngine workflowEngine, ICurrentEmployeeResolver employeeResolver) { _workflowEngine = workflowEngine; _employeeResolver = employeeResolver; }

    public async Task<Guid> Handle(SubmitDesignationChangeRequestCommand request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var payload = new DesignationChangePayload(request.NewDesignationId, request.Reason);
        return await _workflowEngine.SubmitAsync(WorkflowRequestType.DesignationChange, employeeId, payload, ct);
    }
}

public class SubmitTravelRequestCommandHandler : IRequestHandler<SubmitTravelRequestCommand, Guid>
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public SubmitTravelRequestCommandHandler(IWorkflowEngine workflowEngine, ICurrentEmployeeResolver employeeResolver) { _workflowEngine = workflowEngine; _employeeResolver = employeeResolver; }

    public async Task<Guid> Handle(SubmitTravelRequestCommand request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var payload = new TravelRequestPayload(request.StartDate, request.EndDate, request.Destination, request.Purpose, request.EstimatedCost);
        return await _workflowEngine.SubmitAsync(WorkflowRequestType.TravelRequest, employeeId, payload, ct);
    }
}

public class SubmitTravelExpenseCommandHandler : IRequestHandler<SubmitTravelExpenseCommand, Guid>
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public SubmitTravelExpenseCommandHandler(IWorkflowEngine workflowEngine, ICurrentEmployeeResolver employeeResolver) { _workflowEngine = workflowEngine; _employeeResolver = employeeResolver; }

    public async Task<Guid> Handle(SubmitTravelExpenseCommand request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var payload = new TravelExpensePayload(request.TravelRequestId, request.Amount, request.Category, request.Notes, request.ReceiptBlobUrl);
        return await _workflowEngine.SubmitAsync(WorkflowRequestType.TravelExpense, employeeId, payload, ct);
    }
}

public class SubmitExitRequestCommandHandler : IRequestHandler<SubmitExitRequestCommand, Guid>
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public SubmitExitRequestCommandHandler(IWorkflowEngine workflowEngine, ICurrentEmployeeResolver employeeResolver) { _workflowEngine = workflowEngine; _employeeResolver = employeeResolver; }

    public async Task<Guid> Handle(SubmitExitRequestCommand request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var payload = new ExitRequestPayload(request.ProposedLastWorkingDay, request.Reason);
        return await _workflowEngine.SubmitAsync(WorkflowRequestType.ExitRequest, employeeId, payload, ct);
    }
}

/// <summary>
/// Applies the actual domain change for the Phase 2 HR-process request types once their workflow resolves.
/// DepartmentChange/LocationChange/DesignationChange mutate the requester's Employee row directly (mirroring
/// the direct-mutation style of RegularizationResolvedHandler/LeaveRequestResolvedHandler). ExitRequest goes
/// through the existing ExitEmployeeCommand (reused via ISender so its audit-logged reportee-unassignment
/// logic isn't duplicated) and then triggers Full &amp; Final Settlement computation (FR-PAY-18: "Triggered by
/// an approved Exit request"). TravelRequest/TravelExpense have no dedicated domain table in v1 (per
/// 02-data-model-erd.md — payload_json is the only detail store for these types), so approval status alone
/// is sufficient; nothing further to apply here.
/// </summary>
public class HrProcessResolvedHandler : INotificationHandler<WorkflowRequestResolvedNotification>
{
    private readonly AppDbContext _db;
    private readonly ISender _sender;
    public HrProcessResolvedHandler(AppDbContext db, ISender sender) { _db = db; _sender = sender; }

    public async Task Handle(WorkflowRequestResolvedNotification notification, CancellationToken ct)
    {
        if (notification.Status != WorkflowStatus.Approved) return;

        var request = await _db.WorkflowRequests.FindAsync(new object[] { notification.WorkflowRequestId }, ct);
        if (request is null) return;

        switch (notification.RequestType)
        {
            case WorkflowRequestType.DepartmentChange:
            {
                var payload = JsonSerializer.Deserialize<DepartmentChangePayload>(request.PayloadJson)!;
                var employee = await _db.Employees.FindAsync(new object[] { request.RequesterEmployeeId }, ct);
                if (employee is not null) { employee.DepartmentId = payload.NewDepartmentId; await _db.SaveChangesAsync(ct); }
                break;
            }
            case WorkflowRequestType.LocationChange:
            {
                var payload = JsonSerializer.Deserialize<LocationChangePayload>(request.PayloadJson)!;
                var employee = await _db.Employees.FindAsync(new object[] { request.RequesterEmployeeId }, ct);
                if (employee is not null) { employee.LocationId = payload.NewLocationId; await _db.SaveChangesAsync(ct); }
                break;
            }
            case WorkflowRequestType.DesignationChange:
            {
                var payload = JsonSerializer.Deserialize<DesignationChangePayload>(request.PayloadJson)!;
                var employee = await _db.Employees.FindAsync(new object[] { request.RequesterEmployeeId }, ct);
                if (employee is not null) { employee.DesignationId = payload.NewDesignationId; await _db.SaveChangesAsync(ct); }
                break;
            }
            case WorkflowRequestType.ExitRequest:
            {
                var payload = JsonSerializer.Deserialize<ExitRequestPayload>(request.PayloadJson)!;
                await _sender.Send(new Application.Employees.ExitEmployeeCommand(request.RequesterEmployeeId, payload.ProposedLastWorkingDay), ct);
                await _sender.Send(new ComputeFullFinalSettlementCommand(request.RequesterEmployeeId, request.Id), ct);
                await CloneOffboardingChecklistAsync(request.RequesterEmployeeId, payload.ProposedLastWorkingDay, ct);
                break;
            }
            // TravelRequest / TravelExpense: no dedicated table in v1 — approval status alone is sufficient.
        }
    }

    /// <summary>Mirrors ConvertCandidateToEmployeeCommandHandler's onboarding-checklist cloning logic exactly,
    /// for the exit side: clones any OffboardingChecklistTemplate matching the exiting employee's department/
    /// designation into concrete OffboardingTask rows, due-dated relative to the proposed last working day.</summary>
    private async Task CloneOffboardingChecklistAsync(Guid employeeId, DateOnly lastWorkingDay, CancellationToken ct)
    {
        var employee = await _db.Employees.FindAsync(new object[] { employeeId }, ct);
        if (employee is null) return;

        var matchingTemplates = await _db.OffboardingChecklistTemplates
            .Where(t => (t.AppliesToDepartmentId == null || t.AppliesToDepartmentId == employee.DepartmentId)
                     && (t.AppliesToDesignationId == null || t.AppliesToDesignationId == employee.DesignationId))
            .ToListAsync(ct);
        if (matchingTemplates.Count == 0) return;

        var templateIds = matchingTemplates.Select(t => t.Id).ToList();
        var items = await _db.OffboardingChecklistItems.Where(i => templateIds.Contains(i.TemplateId)).ToListAsync(ct);
        foreach (var item in items)
        {
            _db.OffboardingTasks.Add(new Domain.Offboarding.OffboardingTask
            {
                TenantId = employee.TenantId,
                EmployeeId = employeeId,
                Title = item.Title,
                DueDate = lastWorkingDay.AddDays(item.DueOffsetDays),
                Status = Domain.Offboarding.OffboardingTaskStatus.Pending,
                SourceItemId = item.Id
            });
        }
        await _db.SaveChangesAsync(ct);
    }
}
