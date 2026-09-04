using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.OrgStructure;
using PeopleHQ.Domain.Auditing;
using PeopleHQ.Domain.OrgStructure;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.OrgStructure;

// ===== Locations =====
public class CreateLocationCommandHandler : IRequestHandler<CreateLocationCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant; private readonly IAuditLogWriter _audit;
    public CreateLocationCommandHandler(AppDbContext db, ITenantContext tenant, IAuditLogWriter audit) { _db = db; _tenant = tenant; _audit = audit; }

    public async Task<Guid> Handle(CreateLocationCommand request, CancellationToken ct)
    {
        var location = new Location { TenantId = _tenant.TenantId, Name = request.Name, Address = request.Address, TimeZone = request.TimeZone };
        _db.Locations.Add(location);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Location), location.Id, AuditAction.Create, null, location, ct);
        return location.Id;
    }
}

public class UpdateLocationCommandHandler : IRequestHandler<UpdateLocationCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public UpdateLocationCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(UpdateLocationCommand request, CancellationToken ct)
    {
        var location = await _db.Locations.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Location), request.Id);
        var before = new { location.Name, location.Address, location.TimeZone };
        location.Name = request.Name; location.Address = request.Address; location.TimeZone = request.TimeZone;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Location), location.Id, AuditAction.Update, before, location, ct);
    }
}

public class DeleteLocationCommandHandler : IRequestHandler<DeleteLocationCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public DeleteLocationCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(DeleteLocationCommand request, CancellationToken ct)
    {
        var location = await _db.Locations.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Location), request.Id);
        var hasActiveEmployees = await _db.Employees.AnyAsync(e => e.LocationId == request.Id, ct);
        if (hasActiveEmployees) throw new ConflictException($"Location '{location.Name}' has active employees and cannot be deleted.");

        location.IsDeleted = true; location.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Location), location.Id, AuditAction.Delete, location, null, ct);
    }
}

public class GetLocationsQueryHandler : IRequestHandler<GetLocationsQuery, PagedResult<LocationDto>>
{
    private readonly AppDbContext _db;
    public GetLocationsQueryHandler(AppDbContext db) => _db = db;

    public async Task<PagedResult<LocationDto>> Handle(GetLocationsQuery request, CancellationToken ct)
    {
        var query = _db.Locations.OrderBy(l => l.Name);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(l => new LocationDto(l.Id, l.Name, l.Address, l.TimeZone)).ToListAsync(ct);
        return PagedResult<LocationDto>.Create(items, request.Page, request.PageSize, total);
    }
}

// ===== Departments =====
public class CreateDepartmentCommandHandler : IRequestHandler<CreateDepartmentCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant; private readonly IAuditLogWriter _audit;
    public CreateDepartmentCommandHandler(AppDbContext db, ITenantContext tenant, IAuditLogWriter audit) { _db = db; _tenant = tenant; _audit = audit; }

    public async Task<Guid> Handle(CreateDepartmentCommand request, CancellationToken ct)
    {
        var department = new Department { TenantId = _tenant.TenantId, Name = request.Name, ParentDepartmentId = request.ParentDepartmentId, HeadEmployeeId = request.HeadEmployeeId };
        _db.Departments.Add(department);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Department), department.Id, AuditAction.Create, null, department, ct);
        return department.Id;
    }
}

public class UpdateDepartmentCommandHandler : IRequestHandler<UpdateDepartmentCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public UpdateDepartmentCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(UpdateDepartmentCommand request, CancellationToken ct)
    {
        var department = await _db.Departments.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Department), request.Id);
        if (request.ParentDepartmentId == request.Id)
            throw new ValidationException(nameof(request.ParentDepartmentId), "A department cannot be its own parent.");

        var before = new { department.Name, department.ParentDepartmentId, department.HeadEmployeeId };
        department.Name = request.Name; department.ParentDepartmentId = request.ParentDepartmentId; department.HeadEmployeeId = request.HeadEmployeeId;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Department), department.Id, AuditAction.Update, before, department, ct);
    }
}

public class DeleteDepartmentCommandHandler : IRequestHandler<DeleteDepartmentCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public DeleteDepartmentCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(DeleteDepartmentCommand request, CancellationToken ct)
    {
        var department = await _db.Departments.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Department), request.Id);
        var hasActiveEmployees = await _db.Employees.AnyAsync(e => e.DepartmentId == request.Id, ct);
        if (hasActiveEmployees) throw new ConflictException($"Department '{department.Name}' has active employees and cannot be deleted.");

        department.IsDeleted = true; department.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Department), department.Id, AuditAction.Delete, department, null, ct);
    }
}

public class GetDepartmentsQueryHandler : IRequestHandler<GetDepartmentsQuery, PagedResult<DepartmentDto>>
{
    private readonly AppDbContext _db;
    public GetDepartmentsQueryHandler(AppDbContext db) => _db = db;

    public async Task<PagedResult<DepartmentDto>> Handle(GetDepartmentsQuery request, CancellationToken ct)
    {
        var query = _db.Departments.OrderBy(d => d.Name);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(d => new DepartmentDto(d.Id, d.Name, d.ParentDepartmentId, d.HeadEmployeeId)).ToListAsync(ct);
        return PagedResult<DepartmentDto>.Create(items, request.Page, request.PageSize, total);
    }
}

// ===== Designations =====
public class CreateDesignationCommandHandler : IRequestHandler<CreateDesignationCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant; private readonly IAuditLogWriter _audit;
    public CreateDesignationCommandHandler(AppDbContext db, ITenantContext tenant, IAuditLogWriter audit) { _db = db; _tenant = tenant; _audit = audit; }

    public async Task<Guid> Handle(CreateDesignationCommand request, CancellationToken ct)
    {
        var designation = new Designation { TenantId = _tenant.TenantId, Title = request.Title, Grade = request.Grade };
        _db.Designations.Add(designation);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Designation), designation.Id, AuditAction.Create, null, designation, ct);
        return designation.Id;
    }
}

public class UpdateDesignationCommandHandler : IRequestHandler<UpdateDesignationCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public UpdateDesignationCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(UpdateDesignationCommand request, CancellationToken ct)
    {
        var designation = await _db.Designations.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Designation), request.Id);
        var before = new { designation.Title, designation.Grade };
        designation.Title = request.Title; designation.Grade = request.Grade;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Designation), designation.Id, AuditAction.Update, before, designation, ct);
    }
}

public class DeleteDesignationCommandHandler : IRequestHandler<DeleteDesignationCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public DeleteDesignationCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(DeleteDesignationCommand request, CancellationToken ct)
    {
        var designation = await _db.Designations.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Designation), request.Id);
        var hasActiveEmployees = await _db.Employees.AnyAsync(e => e.DesignationId == request.Id, ct);
        if (hasActiveEmployees) throw new ConflictException($"Designation '{designation.Title}' has active employees and cannot be deleted.");

        designation.IsDeleted = true; designation.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Designation), designation.Id, AuditAction.Delete, designation, null, ct);
    }
}

public class GetDesignationsQueryHandler : IRequestHandler<GetDesignationsQuery, PagedResult<DesignationDto>>
{
    private readonly AppDbContext _db;
    public GetDesignationsQueryHandler(AppDbContext db) => _db = db;

    public async Task<PagedResult<DesignationDto>> Handle(GetDesignationsQuery request, CancellationToken ct)
    {
        var query = _db.Designations.OrderBy(d => d.Title);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(d => new DesignationDto(d.Id, d.Title, d.Grade)).ToListAsync(ct);
        return PagedResult<DesignationDto>.Create(items, request.Page, request.PageSize, total);
    }
}

// ===== Org Chart =====
public class GetOrgChartQueryHandler : IRequestHandler<GetOrgChartQuery, IReadOnlyList<OrgChartNodeDto>>
{
    private readonly AppDbContext _db;
    public GetOrgChartQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<OrgChartNodeDto>> Handle(GetOrgChartQuery request, CancellationToken ct)
    {
        // Flat (Id, ManagerId, Name) payload — the tree is assembled client-side (04-frontend-architecture.md).
        return await (
            from e in _db.Employees
            join des in _db.Designations on e.DesignationId equals des.Id into desJoin
            from des in desJoin.DefaultIfEmpty()
            select new OrgChartNodeDto(e.Id, e.FirstName + " " + e.LastName, e.ManagerId, des != null ? des.Title : null)
        ).ToListAsync(ct);
    }
}
