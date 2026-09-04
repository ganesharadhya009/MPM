using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.Employees;
using PeopleHQ.Domain.Auditing;
using PeopleHQ.Domain.Employees;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Employees;

/// <summary>FR-ORG-04: walks the proposed manager's reporting chain to reject A→B→A cycles before a manager assignment is saved.</summary>
public interface IManagerCycleValidator
{
    Task ValidateAsync(Guid employeeId, Guid? proposedManagerId, CancellationToken ct);
}

public class ManagerCycleValidator : IManagerCycleValidator
{
    private readonly AppDbContext _db;
    public ManagerCycleValidator(AppDbContext db) => _db = db;

    public async Task ValidateAsync(Guid employeeId, Guid? proposedManagerId, CancellationToken ct)
    {
        if (proposedManagerId is null) return;
        if (proposedManagerId == employeeId)
            throw new ValidationException(nameof(proposedManagerId), "An employee cannot be their own manager.");

        // Walk up from the proposed manager: if we ever reach `employeeId`, assigning it would create a cycle.
        var currentId = proposedManagerId;
        var visited = new HashSet<Guid>();
        while (currentId is not null)
        {
            if (currentId == employeeId)
                throw new ValidationException(nameof(proposedManagerId), "This assignment would create a reporting cycle.");
            if (!visited.Add(currentId.Value))
                break; // pre-existing cycle unrelated to this change — don't loop forever
            currentId = await _db.Employees.Where(e => e.Id == currentId).Select(e => e.ManagerId).FirstOrDefaultAsync(ct);
        }
    }
}

public class CreateEmployeeCommandHandler : IRequestHandler<CreateEmployeeCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogWriter _audit;
    private readonly IManagerCycleValidator _cycleValidator;

    public CreateEmployeeCommandHandler(AppDbContext db, ITenantContext tenant, IAuditLogWriter audit, IManagerCycleValidator cycleValidator)
    { _db = db; _tenant = tenant; _audit = audit; _cycleValidator = cycleValidator; }

    public async Task<Guid> Handle(CreateEmployeeCommand request, CancellationToken ct)
    {
        var employee = new Employee
        {
            TenantId = _tenant.TenantId,
            EmployeeCode = await GenerateNextEmployeeCodeAsync(ct),
            FirstName = request.FirstName,
            LastName = request.LastName,
            WorkEmail = request.WorkEmail,
            PersonalEmail = request.PersonalEmail,
            Phone = request.Phone,
            DepartmentId = request.DepartmentId,
            LocationId = request.LocationId,
            DesignationId = request.DesignationId,
            ManagerId = request.ManagerId,
            EmploymentType = request.EmploymentType,
            JoinDate = request.JoinDate,
            Status = EmployeeStatus.Active
        };

        if (request.ManagerId is not null)
        {
            // New employee has no Id conflict risk yet, but validate defensively in case a caller reuses an existing Id.
            await _cycleValidator.ValidateAsync(employee.Id, request.ManagerId, ct);
        }

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Employee), employee.Id, AuditAction.Create, null, employee, ct);
        return employee.Id;
    }

    private async Task<string> GenerateNextEmployeeCodeAsync(CancellationToken ct)
    {
        var count = await _db.Employees.IgnoreQueryFilters()
            .Where(e => e.TenantId == _tenant.TenantId)
            .CountAsync(ct);
        return $"EMP{(count + 1):D5}";
    }
}

public class UpdateEmployeeCommandHandler : IRequestHandler<UpdateEmployeeCommand>
{
    private readonly AppDbContext _db;
    private readonly IAuditLogWriter _audit;
    public UpdateEmployeeCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(UpdateEmployeeCommand request, CancellationToken ct)
    {
        var employee = await _db.Employees.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Employee), request.Id);
        var before = new { employee.FirstName, employee.LastName, employee.WorkEmail, employee.PersonalEmail, employee.Phone, employee.DepartmentId, employee.LocationId, employee.DesignationId };

        employee.FirstName = request.FirstName;
        employee.LastName = request.LastName;
        employee.WorkEmail = request.WorkEmail;
        employee.PersonalEmail = request.PersonalEmail;
        employee.Phone = request.Phone;
        employee.DepartmentId = request.DepartmentId;
        employee.LocationId = request.LocationId;
        employee.DesignationId = request.DesignationId;

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Employee), employee.Id, AuditAction.Update, before, employee, ct);
    }
}

public class ChangeEmployeeManagerCommandHandler : IRequestHandler<ChangeEmployeeManagerCommand>
{
    private readonly AppDbContext _db;
    private readonly IAuditLogWriter _audit;
    private readonly IManagerCycleValidator _cycleValidator;
    public ChangeEmployeeManagerCommandHandler(AppDbContext db, IAuditLogWriter audit, IManagerCycleValidator cycleValidator)
    { _db = db; _audit = audit; _cycleValidator = cycleValidator; }

    public async Task Handle(ChangeEmployeeManagerCommand request, CancellationToken ct)
    {
        var employee = await _db.Employees.FindAsync(new object[] { request.EmployeeId }, ct) ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);
        await _cycleValidator.ValidateAsync(employee.Id, request.NewManagerId, ct);

        var before = new { employee.ManagerId };
        employee.ManagerId = request.NewManagerId;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Employee), employee.Id, AuditAction.Update, before, employee, ct);
    }
}

public class ExitEmployeeCommandHandler : IRequestHandler<ExitEmployeeCommand>
{
    private readonly AppDbContext _db;
    private readonly IAuditLogWriter _audit;
    public ExitEmployeeCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(ExitEmployeeCommand request, CancellationToken ct)
    {
        var employee = await _db.Employees.FindAsync(new object[] { request.EmployeeId }, ct) ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);
        var before = new { employee.Status, employee.ExitDate };

        employee.Status = EmployeeStatus.Exited;
        employee.ExitDate = request.ExitDate;

        // Reportees of an exited manager become unmanaged until reassigned — matches FR-ORG-04 intent (no dangling active manager).
        var reportees = await _db.Employees.Where(e => e.ManagerId == employee.Id).ToListAsync(ct);
        foreach (var reportee in reportees) reportee.ManagerId = null;

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Employee), employee.Id, AuditAction.Update, before, employee, ct);
    }
}

public class GetEmployeesQueryHandler : IRequestHandler<GetEmployeesQuery, PagedResult<EmployeeSummaryDto>>
{
    private readonly AppDbContext _db;
    public GetEmployeesQueryHandler(AppDbContext db) => _db = db;

    public async Task<PagedResult<EmployeeSummaryDto>> Handle(GetEmployeesQuery request, CancellationToken ct)
    {
        var query = _db.Employees.AsQueryable();
        if (request.DepartmentId is not null) query = query.Where(e => e.DepartmentId == request.DepartmentId);
        if (request.Status is not null) query = query.Where(e => e.Status == request.Status);
        query = query.OrderBy(e => e.FirstName).ThenBy(e => e.LastName);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(e => new EmployeeSummaryDto(e.Id, e.EmployeeCode, e.FirstName, e.LastName, e.WorkEmail, e.DepartmentId, e.ManagerId, e.Status))
            .ToListAsync(ct);
        return PagedResult<EmployeeSummaryDto>.Create(items, request.Page, request.PageSize, total);
    }
}

public class GetEmployeeByIdQueryHandler : IRequestHandler<GetEmployeeByIdQuery, EmployeeDetailDto>
{
    private readonly AppDbContext _db;
    public GetEmployeeByIdQueryHandler(AppDbContext db) => _db = db;

    public async Task<EmployeeDetailDto> Handle(GetEmployeeByIdQuery request, CancellationToken ct)
    {
        var employee = await _db.Employees.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Employee), request.Id);
        return new EmployeeDetailDto(employee.Id, employee.EmployeeCode, employee.FirstName, employee.LastName, employee.DateOfBirth,
            employee.PersonalEmail, employee.WorkEmail, employee.Phone, employee.DepartmentId, employee.LocationId, employee.DesignationId,
            employee.ManagerId, employee.EmploymentType, employee.JoinDate, employee.ExitDate, employee.Status);
    }
}

public class GetReporteesQueryHandler : IRequestHandler<GetReporteesQuery, IReadOnlyList<EmployeeSummaryDto>>
{
    private readonly AppDbContext _db;
    public GetReporteesQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<EmployeeSummaryDto>> Handle(GetReporteesQuery request, CancellationToken ct)
    {
        if (!request.IncludeIndirect)
        {
            return await _db.Employees.Where(e => e.ManagerId == request.ManagerId)
                .Select(e => new EmployeeSummaryDto(e.Id, e.EmployeeCode, e.FirstName, e.LastName, e.WorkEmail, e.DepartmentId, e.ManagerId, e.Status))
                .ToListAsync(ct);
        }

        // Indirect reportees: BFS down the manager tree in memory (org sizes here don't warrant a recursive CTE).
        var all = await _db.Employees
            .Select(e => new EmployeeSummaryDto(e.Id, e.EmployeeCode, e.FirstName, e.LastName, e.WorkEmail, e.DepartmentId, e.ManagerId, e.Status))
            .ToListAsync(ct);
        var byManager = all.Where(e => e.ManagerId is not null).ToLookup(e => e.ManagerId!.Value);

        var result = new List<EmployeeSummaryDto>();
        var frontier = new Queue<Guid>();
        frontier.Enqueue(request.ManagerId);
        while (frontier.Count > 0)
        {
            var managerId = frontier.Dequeue();
            foreach (var direct in byManager[managerId])
            {
                result.Add(direct);
                frontier.Enqueue(direct.Id);
            }
        }
        return result;
    }
}
