using MediatR;
using PeopleHQ.Application.Common;

namespace PeopleHQ.Application.OrgStructure;

// --- Locations ---
public record CreateLocationCommand(string Name, string? Address, string TimeZone) : IRequest<Guid>;
public record UpdateLocationCommand(Guid Id, string Name, string? Address, string TimeZone) : IRequest;
public record DeleteLocationCommand(Guid Id) : IRequest;
public record GetLocationsQuery(int Page = 1, int PageSize = 25) : IRequest<PagedResult<LocationDto>>;
public record LocationDto(Guid Id, string Name, string? Address, string TimeZone);

// --- Departments ---
public record CreateDepartmentCommand(string Name, Guid? ParentDepartmentId, Guid? HeadEmployeeId) : IRequest<Guid>;
public record UpdateDepartmentCommand(Guid Id, string Name, Guid? ParentDepartmentId, Guid? HeadEmployeeId) : IRequest;
public record DeleteDepartmentCommand(Guid Id) : IRequest;
public record GetDepartmentsQuery(int Page = 1, int PageSize = 25) : IRequest<PagedResult<DepartmentDto>>;
public record DepartmentDto(Guid Id, string Name, Guid? ParentDepartmentId, Guid? HeadEmployeeId);

// --- Designations ---
public record CreateDesignationCommand(string Title, int? Grade) : IRequest<Guid>;
public record UpdateDesignationCommand(Guid Id, string Title, int? Grade) : IRequest;
public record DeleteDesignationCommand(Guid Id) : IRequest;
public record GetDesignationsQuery(int Page = 1, int PageSize = 25) : IRequest<PagedResult<DesignationDto>>;
public record DesignationDto(Guid Id, string Title, int? Grade);

// --- Org Chart ---
public record GetOrgChartQuery : IRequest<IReadOnlyList<OrgChartNodeDto>>;
public record OrgChartNodeDto(Guid Id, string FullName, Guid? ManagerId, string? DesignationTitle);
