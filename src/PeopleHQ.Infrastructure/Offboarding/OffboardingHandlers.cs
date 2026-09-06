using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.Offboarding;
using PeopleHQ.Domain.Auditing;
using PeopleHQ.Domain.Offboarding;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Offboarding;

// ===== Checklist Templates =====
public class CreateOffboardingChecklistTemplateCommandHandler : IRequestHandler<CreateOffboardingChecklistTemplateCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant; private readonly IAuditLogWriter _audit;
    public CreateOffboardingChecklistTemplateCommandHandler(AppDbContext db, ITenantContext tenant, IAuditLogWriter audit) { _db = db; _tenant = tenant; _audit = audit; }

    public async Task<Guid> Handle(CreateOffboardingChecklistTemplateCommand request, CancellationToken ct)
    {
        var template = new OffboardingChecklistTemplate
        {
            TenantId = _tenant.TenantId, Name = request.Name,
            AppliesToDepartmentId = request.AppliesToDepartmentId, AppliesToDesignationId = request.AppliesToDesignationId
        };
        _db.OffboardingChecklistTemplates.Add(template);
        await _db.SaveChangesAsync(ct); // need template.Id for items

        foreach (var item in request.Items)
        {
            _db.OffboardingChecklistItems.Add(new OffboardingChecklistItem
            {
                TemplateId = template.Id, Title = item.Title, OwnerRole = item.OwnerRole, DueOffsetDays = item.DueOffsetDays
            });
        }
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(OffboardingChecklistTemplate), template.Id, AuditAction.Create, null, template, ct);
        return template.Id;
    }
}

public class UpdateOffboardingChecklistTemplateCommandHandler : IRequestHandler<UpdateOffboardingChecklistTemplateCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public UpdateOffboardingChecklistTemplateCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(UpdateOffboardingChecklistTemplateCommand request, CancellationToken ct)
    {
        var template = await _db.OffboardingChecklistTemplates.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(OffboardingChecklistTemplate), request.Id);
        var before = new { template.Name, template.AppliesToDepartmentId, template.AppliesToDesignationId };
        template.Name = request.Name; template.AppliesToDepartmentId = request.AppliesToDepartmentId; template.AppliesToDesignationId = request.AppliesToDesignationId;

        var existingItems = await _db.OffboardingChecklistItems.Where(i => i.TemplateId == template.Id).ToListAsync(ct);
        _db.OffboardingChecklistItems.RemoveRange(existingItems);
        foreach (var item in request.Items)
        {
            _db.OffboardingChecklistItems.Add(new OffboardingChecklistItem
            {
                TemplateId = template.Id, Title = item.Title, OwnerRole = item.OwnerRole, DueOffsetDays = item.DueOffsetDays
            });
        }

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(OffboardingChecklistTemplate), template.Id, AuditAction.Update, before, template, ct);
    }
}

public class DeleteOffboardingChecklistTemplateCommandHandler : IRequestHandler<DeleteOffboardingChecklistTemplateCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public DeleteOffboardingChecklistTemplateCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(DeleteOffboardingChecklistTemplateCommand request, CancellationToken ct)
    {
        var template = await _db.OffboardingChecklistTemplates.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(OffboardingChecklistTemplate), request.Id);
        var items = await _db.OffboardingChecklistItems.Where(i => i.TemplateId == template.Id).ToListAsync(ct);
        _db.OffboardingChecklistItems.RemoveRange(items);

        template.IsDeleted = true; template.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(OffboardingChecklistTemplate), template.Id, AuditAction.Delete, template, null, ct);
    }
}

public class GetOffboardingChecklistTemplatesQueryHandler : IRequestHandler<GetOffboardingChecklistTemplatesQuery, IReadOnlyList<OffboardingChecklistTemplateDto>>
{
    private readonly AppDbContext _db;
    public GetOffboardingChecklistTemplatesQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<OffboardingChecklistTemplateDto>> Handle(GetOffboardingChecklistTemplatesQuery request, CancellationToken ct)
    {
        var templates = await _db.OffboardingChecklistTemplates.OrderBy(t => t.Name).ToListAsync(ct);
        var templateIds = templates.Select(t => t.Id).ToList();
        var itemsByTemplate = (await _db.OffboardingChecklistItems.Where(i => templateIds.Contains(i.TemplateId)).ToListAsync(ct))
            .ToLookup(i => i.TemplateId);

        return templates.Select(t => new OffboardingChecklistTemplateDto(
            t.Id, t.Name, t.AppliesToDepartmentId, t.AppliesToDesignationId,
            itemsByTemplate[t.Id].Select(i => new OffboardingChecklistItemDto(i.Id, i.Title, i.OwnerRole, i.DueOffsetDays)).ToList()
        )).ToList();
    }
}

// ===== Offboarding Tasks =====
public class CompleteOffboardingTaskCommandHandler : IRequestHandler<CompleteOffboardingTaskCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public CompleteOffboardingTaskCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(CompleteOffboardingTaskCommand request, CancellationToken ct)
    {
        var task = await _db.OffboardingTasks.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(OffboardingTask), request.Id);
        var before = new { task.Status };
        task.Status = OffboardingTaskStatus.Done;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(OffboardingTask), task.Id, AuditAction.Update, before, task, ct);
    }
}

public class GetOffboardingTasksQueryHandler : IRequestHandler<GetOffboardingTasksQuery, IReadOnlyList<OffboardingTaskDto>>
{
    private readonly AppDbContext _db;
    public GetOffboardingTasksQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<OffboardingTaskDto>> Handle(GetOffboardingTasksQuery request, CancellationToken ct)
    {
        var query = _db.OffboardingTasks.AsQueryable();
        if (request.EmployeeId is not null) query = query.Where(t => t.EmployeeId == request.EmployeeId);
        if (request.Status is not null) query = query.Where(t => t.Status == request.Status);

        return await query.OrderBy(t => t.DueDate)
            .Select(t => new OffboardingTaskDto(t.Id, t.EmployeeId, t.Title, t.OwnerEmployeeId, t.DueDate, t.Status))
            .ToListAsync(ct);
    }
}
