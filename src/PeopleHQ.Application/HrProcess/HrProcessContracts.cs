using MediatR;

namespace PeopleHQ.Application.HrProcess;

// HR Process requests (01-modules-functional-spec.md §H/§J, Phase 2): each is a small self-service form
// routed through the generic Workflow engine as its own WorkflowRequestType. All commands here always act
// on the CALLER's own employee id (resolved server-side via ICurrentEmployeeResolver) — there is no target
// EmployeeId parameter, so there is no IDOR surface in this module. Approval/rejection/withdrawal reuse the
// existing generic Approvals endpoints (PeopleHQ.Application.Workflow) — one inbox across all request types,
// per spec §H "do not scatter approvals across separate module-specific inboxes".

public record SubmitDepartmentChangeRequestCommand(Guid NewDepartmentId, string? Reason) : IRequest<Guid>;
public record SubmitLocationChangeRequestCommand(Guid NewLocationId, string? Reason) : IRequest<Guid>;
public record SubmitDesignationChangeRequestCommand(Guid NewDesignationId, string? Reason) : IRequest<Guid>;
public record SubmitTravelRequestCommand(DateOnly StartDate, DateOnly EndDate, string Destination, string Purpose, decimal? EstimatedCost) : IRequest<Guid>;
public record SubmitTravelExpenseCommand(Guid? TravelRequestId, decimal Amount, string Category, string? Notes, string? ReceiptBlobUrl) : IRequest<Guid>;
public record SubmitExitRequestCommand(DateOnly ProposedLastWorkingDay, string Reason) : IRequest<Guid>;

// Payload shapes serialized into WorkflowRequest.PayloadJson at submission time and deserialized by
// HrProcessResolvedHandler on approval to apply the actual domain change.
public record DepartmentChangePayload(Guid NewDepartmentId, string? Reason);
public record LocationChangePayload(Guid NewLocationId, string? Reason);
public record DesignationChangePayload(Guid NewDesignationId, string? Reason);
public record TravelRequestPayload(DateOnly StartDate, DateOnly EndDate, string Destination, string Purpose, decimal? EstimatedCost);
public record TravelExpensePayload(Guid? TravelRequestId, decimal Amount, string Category, string? Notes, string? ReceiptBlobUrl);
public record ExitRequestPayload(DateOnly ProposedLastWorkingDay, string Reason);
