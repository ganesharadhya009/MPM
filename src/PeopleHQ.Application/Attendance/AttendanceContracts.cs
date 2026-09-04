using MediatR;
using PeopleHQ.Application.Common;
using PeopleHQ.Domain.Attendance;

namespace PeopleHQ.Application.Attendance;

// --- Shifts ---
public record CreateShiftCommand(string Name, TimeOnly StartTime, TimeOnly EndTime, int GraceMinutes, int BreakMinutes) : IRequest<Guid>;
public record UpdateShiftCommand(Guid Id, string Name, TimeOnly StartTime, TimeOnly EndTime, int GraceMinutes, int BreakMinutes) : IRequest;
public record DeleteShiftCommand(Guid Id) : IRequest;
public record GetShiftsQuery(int Page = 1, int PageSize = 25) : IRequest<PagedResult<ShiftDto>>;
public record ShiftDto(Guid Id, string Name, TimeOnly StartTime, TimeOnly EndTime, int GraceMinutes, int BreakMinutes);

public record AssignShiftCommand(Guid EmployeeId, Guid ShiftId, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest<Guid>;
public record GetShiftAssignmentsQuery(Guid EmployeeId) : IRequest<IReadOnlyList<ShiftAssignmentDto>>;
public record ShiftAssignmentDto(Guid Id, Guid EmployeeId, Guid ShiftId, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

// --- Check-in/out (FR-ATT) ---
public record CheckInCommand(double? Lat, double? Lng) : IRequest<Guid>;
public record CheckOutCommand : IRequest;
public record GetAttendanceQuery(Guid? EmployeeId = null, DateOnly? DateFrom = null, DateOnly? DateTo = null, int Page = 1, int PageSize = 25)
    : IRequest<PagedResult<AttendanceRecordDto>>;
public record AttendanceRecordDto(Guid Id, Guid EmployeeId, DateOnly Date, DateTime? CheckInAtUtc, DateTime? CheckOutAtUtc,
    AttendanceSource Source, AttendanceStatus Status, decimal OvertimeHours);

// --- Regularization (FR-ATT-09), routed through the generic Workflow engine ---
public record CreateRegularizationRequestCommand(Guid AttendanceRecordId, DateTime? RequestedCheckInAtUtc, DateTime? RequestedCheckOutAtUtc, string Reason) : IRequest<Guid>;
public record GetRegularizationRequestsQuery(Guid? EmployeeId = null, RegularizationStatus? Status = null) : IRequest<IReadOnlyList<RegularizationRequestDto>>;
public record RegularizationRequestDto(Guid Id, Guid AttendanceRecordId, Guid EmployeeId, DateTime? RequestedCheckInAtUtc,
    DateTime? RequestedCheckOutAtUtc, string Reason, RegularizationStatus Status, Guid? WorkflowRequestId);
