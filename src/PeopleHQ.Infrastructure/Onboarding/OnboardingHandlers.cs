using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.Onboarding;
using PeopleHQ.Domain.Auditing;
using PeopleHQ.Domain.Employees;
using PeopleHQ.Domain.Onboarding;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Onboarding;

// ===== Candidates =====
public class CreateCandidateCommandHandler : IRequestHandler<CreateCandidateCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant; private readonly IAuditLogWriter _audit;
    public CreateCandidateCommandHandler(AppDbContext db, ITenantContext tenant, IAuditLogWriter audit) { _db = db; _tenant = tenant; _audit = audit; }

    public async Task<Guid> Handle(CreateCandidateCommand request, CancellationToken ct)
    {
        var candidate = new Candidate
        {
            TenantId = _tenant.TenantId, Name = request.Name, Email = request.Email, Phone = request.Phone,
            ResumeBlobUrl = request.ResumeBlobUrl, DesignationId = request.DesignationId, Source = request.Source
        };
        _db.Candidates.Add(candidate);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Candidate), candidate.Id, AuditAction.Create, null, candidate, ct);
        return candidate.Id;
    }
}

public class UpdateCandidateCommandHandler : IRequestHandler<UpdateCandidateCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public UpdateCandidateCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(UpdateCandidateCommand request, CancellationToken ct)
    {
        var candidate = await _db.Candidates.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Candidate), request.Id);
        var before = new { candidate.Name, candidate.Phone, candidate.ResumeBlobUrl, candidate.DesignationId, candidate.Source };
        candidate.Name = request.Name; candidate.Phone = request.Phone; candidate.ResumeBlobUrl = request.ResumeBlobUrl;
        candidate.DesignationId = request.DesignationId; candidate.Source = request.Source;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Candidate), candidate.Id, AuditAction.Update, before, candidate, ct);
    }
}

public class UpdateCandidateStageCommandHandler : IRequestHandler<UpdateCandidateStageCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public UpdateCandidateStageCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(UpdateCandidateStageCommand request, CancellationToken ct)
    {
        var candidate = await _db.Candidates.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Candidate), request.Id);
        if (candidate.Stage == CandidateStage.Converted)
            throw new ConflictException("A converted candidate's stage cannot be changed further.");

        var before = new { candidate.Stage };
        candidate.Stage = request.Stage;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Candidate), candidate.Id, AuditAction.Update, before, candidate, ct);
    }
}

public class GetCandidatesQueryHandler : IRequestHandler<GetCandidatesQuery, PagedResult<CandidateDto>>
{
    private readonly AppDbContext _db;
    public GetCandidatesQueryHandler(AppDbContext db) => _db = db;

    public async Task<PagedResult<CandidateDto>> Handle(GetCandidatesQuery request, CancellationToken ct)
    {
        var query = _db.Candidates.AsQueryable();
        if (request.Stage is not null) query = query.Where(c => c.Stage == request.Stage);
        query = query.OrderByDescending(c => c.CreatedAtUtc);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(c => new CandidateDto(c.Id, c.Name, c.Email, c.Phone, c.ResumeBlobUrl, c.DesignationId, c.Source, c.Stage, c.ConvertedEmployeeId))
            .ToListAsync(ct);
        return PagedResult<CandidateDto>.Create(items, request.Page, request.PageSize, total);
    }
}

public class GetCandidateByIdQueryHandler : IRequestHandler<GetCandidateByIdQuery, CandidateDto>
{
    private readonly AppDbContext _db;
    public GetCandidateByIdQueryHandler(AppDbContext db) => _db = db;

    public async Task<CandidateDto> Handle(GetCandidateByIdQuery request, CancellationToken ct)
    {
        var c = await _db.Candidates.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Candidate), request.Id);
        return new CandidateDto(c.Id, c.Name, c.Email, c.Phone, c.ResumeBlobUrl, c.DesignationId, c.Source, c.Stage, c.ConvertedEmployeeId);
    }
}

/// <summary>FR-ONB-05/06: creates the Employee record from a candidate and clones any matching checklist
/// template's items into concrete OnboardingTask rows due-dated relative to the join date.</summary>
public class ConvertCandidateToEmployeeCommandHandler : IRequestHandler<ConvertCandidateToEmployeeCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant; private readonly IAuditLogWriter _audit;
    public ConvertCandidateToEmployeeCommandHandler(AppDbContext db, ITenantContext tenant, IAuditLogWriter audit) { _db = db; _tenant = tenant; _audit = audit; }

    public async Task<Guid> Handle(ConvertCandidateToEmployeeCommand request, CancellationToken ct)
    {
        var candidate = await _db.Candidates.FindAsync(new object[] { request.CandidateId }, ct) ?? throw new NotFoundException(nameof(Candidate), request.CandidateId);
        if (candidate.Stage == CandidateStage.Converted)
            throw new ConflictException("This candidate has already been converted.");
        if (candidate.Stage == CandidateStage.Rejected)
            throw new ConflictException("A rejected candidate cannot be converted.");

        var nameParts = candidate.Name.Split(' ', 2);
        var employeeCount = await _db.Employees.IgnoreQueryFilters().Where(e => e.TenantId == _tenant.TenantId).CountAsync(ct);

        var employee = new Employee
        {
            TenantId = _tenant.TenantId,
            EmployeeCode = $"EMP{(employeeCount + 1):D5}",
            FirstName = nameParts[0],
            LastName = nameParts.Length > 1 ? nameParts[1] : string.Empty,
            PersonalEmail = candidate.Email,
            WorkEmail = request.WorkEmail,
            Phone = candidate.Phone,
            DepartmentId = request.DepartmentId,
            LocationId = request.LocationId,
            DesignationId = candidate.DesignationId,
            ManagerId = request.ManagerId,
            JoinDate = request.JoinDate,
            Status = EmployeeStatus.Active
        };
        _db.Employees.Add(employee);

        candidate.Stage = CandidateStage.Converted;
        candidate.ConvertedEmployeeId = employee.Id;

        var matchingTemplates = await _db.OnboardingChecklistTemplates
            .Where(t => (t.AppliesToDepartmentId == null || t.AppliesToDepartmentId == request.DepartmentId)
                     && (t.AppliesToDesignationId == null || t.AppliesToDesignationId == candidate.DesignationId))
            .ToListAsync(ct);

        if (matchingTemplates.Count > 0)
        {
            var templateIds = matchingTemplates.Select(t => t.Id).ToList();
            var items = await _db.OnboardingChecklistItems.Where(i => templateIds.Contains(i.TemplateId)).ToListAsync(ct);
            foreach (var item in items)
            {
                _db.OnboardingTasks.Add(new Domain.Onboarding.OnboardingTask
                {
                    TenantId = _tenant.TenantId,
                    EmployeeId = employee.Id,
                    Title = item.Title,
                    DueDate = request.JoinDate.AddDays(item.DueOffsetDays),
                    Status = OnboardingTaskStatus.Pending,
                    SourceItemId = item.Id
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Employee), employee.Id, AuditAction.Create, null, employee, ct);
        await _audit.WriteAsync(nameof(Candidate), candidate.Id, AuditAction.Update, null, candidate, ct);
        return employee.Id;
    }
}

// ===== Checklist Templates =====
public class CreateOnboardingChecklistTemplateCommandHandler : IRequestHandler<CreateOnboardingChecklistTemplateCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant; private readonly IAuditLogWriter _audit;
    public CreateOnboardingChecklistTemplateCommandHandler(AppDbContext db, ITenantContext tenant, IAuditLogWriter audit) { _db = db; _tenant = tenant; _audit = audit; }

    public async Task<Guid> Handle(CreateOnboardingChecklistTemplateCommand request, CancellationToken ct)
    {
        var template = new OnboardingChecklistTemplate
        {
            TenantId = _tenant.TenantId, Name = request.Name,
            AppliesToDepartmentId = request.AppliesToDepartmentId, AppliesToDesignationId = request.AppliesToDesignationId
        };
        _db.OnboardingChecklistTemplates.Add(template);
        await _db.SaveChangesAsync(ct); // need template.Id for items

        foreach (var item in request.Items)
        {
            _db.OnboardingChecklistItems.Add(new OnboardingChecklistItem
            {
                TemplateId = template.Id, Title = item.Title, OwnerRole = item.OwnerRole,
                DueOffsetDays = item.DueOffsetDays, BuddyRole = item.BuddyRole
            });
        }
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(OnboardingChecklistTemplate), template.Id, AuditAction.Create, null, template, ct);
        return template.Id;
    }
}

public class UpdateOnboardingChecklistTemplateCommandHandler : IRequestHandler<UpdateOnboardingChecklistTemplateCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public UpdateOnboardingChecklistTemplateCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(UpdateOnboardingChecklistTemplateCommand request, CancellationToken ct)
    {
        var template = await _db.OnboardingChecklistTemplates.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(OnboardingChecklistTemplate), request.Id);
        var before = new { template.Name, template.AppliesToDepartmentId, template.AppliesToDesignationId };
        template.Name = request.Name; template.AppliesToDepartmentId = request.AppliesToDepartmentId; template.AppliesToDesignationId = request.AppliesToDesignationId;

        var existingItems = await _db.OnboardingChecklistItems.Where(i => i.TemplateId == template.Id).ToListAsync(ct);
        _db.OnboardingChecklistItems.RemoveRange(existingItems);
        foreach (var item in request.Items)
        {
            _db.OnboardingChecklistItems.Add(new OnboardingChecklistItem
            {
                TemplateId = template.Id, Title = item.Title, OwnerRole = item.OwnerRole,
                DueOffsetDays = item.DueOffsetDays, BuddyRole = item.BuddyRole
            });
        }

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(OnboardingChecklistTemplate), template.Id, AuditAction.Update, before, template, ct);
    }
}

public class DeleteOnboardingChecklistTemplateCommandHandler : IRequestHandler<DeleteOnboardingChecklistTemplateCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public DeleteOnboardingChecklistTemplateCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(DeleteOnboardingChecklistTemplateCommand request, CancellationToken ct)
    {
        var template = await _db.OnboardingChecklistTemplates.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(OnboardingChecklistTemplate), request.Id);
        var items = await _db.OnboardingChecklistItems.Where(i => i.TemplateId == template.Id).ToListAsync(ct);
        _db.OnboardingChecklistItems.RemoveRange(items);

        template.IsDeleted = true; template.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(OnboardingChecklistTemplate), template.Id, AuditAction.Delete, template, null, ct);
    }
}

public class GetOnboardingChecklistTemplatesQueryHandler : IRequestHandler<GetOnboardingChecklistTemplatesQuery, PagedResult<OnboardingChecklistTemplateDto>>
{
    private readonly AppDbContext _db;
    public GetOnboardingChecklistTemplatesQueryHandler(AppDbContext db) => _db = db;

    public async Task<PagedResult<OnboardingChecklistTemplateDto>> Handle(GetOnboardingChecklistTemplatesQuery request, CancellationToken ct)
    {
        var query = _db.OnboardingChecklistTemplates.OrderBy(t => t.Name);
        var total = await query.CountAsync(ct);
        var templates = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        var templateIds = templates.Select(t => t.Id).ToList();
        var itemsByTemplate = (await _db.OnboardingChecklistItems.Where(i => templateIds.Contains(i.TemplateId)).ToListAsync(ct))
            .ToLookup(i => i.TemplateId);

        var dtos = templates.Select(t => new OnboardingChecklistTemplateDto(
            t.Id, t.Name, t.AppliesToDepartmentId, t.AppliesToDesignationId,
            itemsByTemplate[t.Id].Select(i => new OnboardingChecklistItemDto(i.Id, i.Title, i.OwnerRole, i.DueOffsetDays, i.BuddyRole)).ToList()
        )).ToList();
        return PagedResult<OnboardingChecklistTemplateDto>.Create(dtos, request.Page, request.PageSize, total);
    }
}

// ===== Onboarding Tasks =====
public class CreateOnboardingTaskCommandHandler : IRequestHandler<CreateOnboardingTaskCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant; private readonly IAuditLogWriter _audit;
    public CreateOnboardingTaskCommandHandler(AppDbContext db, ITenantContext tenant, IAuditLogWriter audit) { _db = db; _tenant = tenant; _audit = audit; }

    public async Task<Guid> Handle(CreateOnboardingTaskCommand request, CancellationToken ct)
    {
        var task = new Domain.Onboarding.OnboardingTask
        {
            TenantId = _tenant.TenantId, CandidateId = request.CandidateId, EmployeeId = request.EmployeeId,
            Title = request.Title, OwnerEmployeeId = request.OwnerEmployeeId, DueDate = request.DueDate
        };
        _db.OnboardingTasks.Add(task);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Domain.Onboarding.OnboardingTask), task.Id, AuditAction.Create, null, task, ct);
        return task.Id;
    }
}

public class CompleteOnboardingTaskCommandHandler : IRequestHandler<CompleteOnboardingTaskCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public CompleteOnboardingTaskCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(CompleteOnboardingTaskCommand request, CancellationToken ct)
    {
        var task = await _db.OnboardingTasks.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Domain.Onboarding.OnboardingTask), request.Id);
        var before = new { task.Status };
        task.Status = OnboardingTaskStatus.Done;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Domain.Onboarding.OnboardingTask), task.Id, AuditAction.Update, before, task, ct);
    }
}

public class GetOnboardingTasksQueryHandler : IRequestHandler<GetOnboardingTasksQuery, IReadOnlyList<OnboardingTaskDto>>
{
    private readonly AppDbContext _db;
    public GetOnboardingTasksQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<OnboardingTaskDto>> Handle(GetOnboardingTasksQuery request, CancellationToken ct)
    {
        var query = _db.OnboardingTasks.AsQueryable();
        if (request.CandidateId is not null) query = query.Where(t => t.CandidateId == request.CandidateId);
        if (request.EmployeeId is not null) query = query.Where(t => t.EmployeeId == request.EmployeeId);
        if (request.Status is not null) query = query.Where(t => t.Status == request.Status);

        return await query.OrderBy(t => t.DueDate)
            .Select(t => new OnboardingTaskDto(t.Id, t.CandidateId, t.EmployeeId, t.Title, t.OwnerEmployeeId, t.DueDate, t.Status))
            .ToListAsync(ct);
    }
}
