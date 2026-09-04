using MediatR;
using PeopleHQ.Application.Common;
using PeopleHQ.Domain.Employees;

namespace PeopleHQ.Application.Employees;

public record CreateEmployeeCommand(
    string FirstName, string LastName, string? WorkEmail, string? PersonalEmail, string? Phone,
    Guid? DepartmentId, Guid? LocationId, Guid? DesignationId, Guid? ManagerId,
    EmploymentType EmploymentType, DateOnly JoinDate) : IRequest<Guid>;

public record UpdateEmployeeCommand(
    Guid Id, string FirstName, string LastName, string? WorkEmail, string? PersonalEmail, string? Phone,
    Guid? DepartmentId, Guid? LocationId, Guid? DesignationId) : IRequest;

public record ChangeEmployeeManagerCommand(Guid EmployeeId, Guid? NewManagerId) : IRequest;

public record ExitEmployeeCommand(Guid EmployeeId, DateOnly ExitDate) : IRequest;

public record GetEmployeesQuery(int Page = 1, int PageSize = 25, Guid? DepartmentId = null, EmployeeStatus? Status = null)
    : IRequest<PagedResult<EmployeeSummaryDto>>;

public record GetEmployeeByIdQuery(Guid Id) : IRequest<EmployeeDetailDto>;
public record GetReporteesQuery(Guid ManagerId, bool IncludeIndirect = false) : IRequest<IReadOnlyList<EmployeeSummaryDto>>;

public record EmployeeSummaryDto(
    Guid Id, string EmployeeCode, string FirstName, string LastName, string? WorkEmail,
    Guid? DepartmentId, Guid? ManagerId, EmployeeStatus Status);

public record EmployeeDetailDto(
    Guid Id, string EmployeeCode, string FirstName, string LastName, DateOnly? DateOfBirth,
    string? PersonalEmail, string? WorkEmail, string? Phone,
    Guid? DepartmentId, Guid? LocationId, Guid? DesignationId, Guid? ManagerId,
    EmploymentType EmploymentType, DateOnly JoinDate, DateOnly? ExitDate, EmployeeStatus Status);
