using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.Performance;
using PeopleHQ.Domain.Employees;
using PeopleHQ.Domain.Performance;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Performance;

/// <summary>Shared ownership check for Goals/Objectives: the caller may act on their own record, or on a
/// direct report's record (manager scope) — matches "manager can add goals for reportees" (§I). Does not
/// walk the full reporting chain; direct reports only, consistent with the rest of the codebase's
/// manager-scoped checks (e.g. RegularizationApprove).</summary>
internal static class OwnershipHelper
{
    public static async Task<bool> IsSelfOrDirectManagerAsync(AppDbContext db, Guid targetEmployeeId, Guid callerEmployeeId, CancellationToken ct)
    {
        if (targetEmployeeId == callerEmployeeId) return true;
        var target = await db.Employees.FindAsync(new object[] { targetEmployeeId }, ct);
        return target?.ManagerId == callerEmployeeId;
    }
}

public class CreateGoalCommandHandler : IRequestHandler<CreateGoalCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public CreateGoalCommandHandler(AppDbContext db, ITenantContext tenant, ICurrentEmployeeResolver employeeResolver) { _db = db; _tenant = tenant; _employeeResolver = employeeResolver; }

    public async Task<Guid> Handle(CreateGoalCommand request, CancellationToken ct)
    {
        var callerId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        if (!await OwnershipHelper.IsSelfOrDirectManagerAsync(_db, request.EmployeeId, callerId, ct))
            throw new ForbiddenException("You can only create goals for yourself or your direct reports.");

        var goal = new Goal
        {
            TenantId = _tenant.TenantId,
            EmployeeId = request.EmployeeId,
            Title = request.Title,
            Description = request.Description,
            TargetDate = request.TargetDate
        };
        _db.Goals.Add(goal);
        await _db.SaveChangesAsync(ct);
        return goal.Id;
    }
}

public class UpdateGoalCommandHandler : IRequestHandler<UpdateGoalCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public UpdateGoalCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task Handle(UpdateGoalCommand request, CancellationToken ct)
    {
        var goal = await _db.Goals.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Goal), request.Id);
        var callerId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        if (!await OwnershipHelper.IsSelfOrDirectManagerAsync(_db, goal.EmployeeId, callerId, ct))
            throw new ForbiddenException("You can only update your own goals or your direct reports' goals.");

        goal.Title = request.Title;
        goal.Description = request.Description;
        goal.TargetDate = request.TargetDate;
        goal.ProgressPercent = request.ProgressPercent;
        goal.Status = request.Status;
        await _db.SaveChangesAsync(ct);
    }
}

public class DeleteGoalCommandHandler : IRequestHandler<DeleteGoalCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public DeleteGoalCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task Handle(DeleteGoalCommand request, CancellationToken ct)
    {
        var goal = await _db.Goals.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Goal), request.Id);
        var callerId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        if (!await OwnershipHelper.IsSelfOrDirectManagerAsync(_db, goal.EmployeeId, callerId, ct))
            throw new ForbiddenException("You can only delete your own goals or your direct reports' goals.");

        goal.IsDeleted = true;
        goal.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetGoalsQueryHandler : IRequestHandler<GetGoalsQuery, IReadOnlyList<GoalDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public GetGoalsQueryHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task<IReadOnlyList<GoalDto>> Handle(GetGoalsQuery request, CancellationToken ct)
    {
        var callerId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var targetEmployeeId = request.EmployeeId ?? callerId;
        if (!await OwnershipHelper.IsSelfOrDirectManagerAsync(_db, targetEmployeeId, callerId, ct))
            throw new ForbiddenException("You can only view your own goals or your direct reports' goals.");

        return await _db.Goals.Where(g => g.EmployeeId == targetEmployeeId)
            .Select(g => new GoalDto(g.Id, g.EmployeeId, g.Title, g.Description, g.TargetDate, g.ProgressPercent, g.Status))
            .ToListAsync(ct);
    }
}

public class CreateOkrCycleCommandHandler : IRequestHandler<CreateOkrCycleCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public CreateOkrCycleCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<Guid> Handle(CreateOkrCycleCommand request, CancellationToken ct)
    {
        var cycle = new OkrCycle { TenantId = _tenant.TenantId, Name = request.Name, StartDate = request.StartDate, EndDate = request.EndDate };
        _db.OkrCycles.Add(cycle);
        await _db.SaveChangesAsync(ct);
        return cycle.Id;
    }
}

public class UpdateOkrCycleCommandHandler : IRequestHandler<UpdateOkrCycleCommand>
{
    private readonly AppDbContext _db;
    public UpdateOkrCycleCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(UpdateOkrCycleCommand request, CancellationToken ct)
    {
        var cycle = await _db.OkrCycles.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(OkrCycle), request.Id);
        cycle.Name = request.Name;
        cycle.StartDate = request.StartDate;
        cycle.EndDate = request.EndDate;
        await _db.SaveChangesAsync(ct);
    }
}

public class DeleteOkrCycleCommandHandler : IRequestHandler<DeleteOkrCycleCommand>
{
    private readonly AppDbContext _db;
    public DeleteOkrCycleCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(DeleteOkrCycleCommand request, CancellationToken ct)
    {
        var cycle = await _db.OkrCycles.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(OkrCycle), request.Id);
        var inUse = await _db.Objectives.AnyAsync(o => o.CycleId == request.Id, ct);
        if (inUse) throw new ConflictException("This OKR cycle has objectives and cannot be deleted.");

        cycle.IsDeleted = true;
        cycle.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetOkrCyclesQueryHandler : IRequestHandler<GetOkrCyclesQuery, IReadOnlyList<OkrCycleDto>>
{
    private readonly AppDbContext _db;
    public GetOkrCyclesQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<OkrCycleDto>> Handle(GetOkrCyclesQuery request, CancellationToken ct)
        => await _db.OkrCycles.OrderByDescending(c => c.StartDate)
            .Select(c => new OkrCycleDto(c.Id, c.Name, c.StartDate, c.EndDate))
            .ToListAsync(ct);
}

public class CreateObjectiveCommandHandler : IRequestHandler<CreateObjectiveCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly IPermissionChecker _permissionChecker;
    public CreateObjectiveCommandHandler(AppDbContext db, ITenantContext tenant, ICurrentEmployeeResolver employeeResolver, IPermissionChecker permissionChecker)
    { _db = db; _tenant = tenant; _employeeResolver = employeeResolver; _permissionChecker = permissionChecker; }

    public async Task<Guid> Handle(CreateObjectiveCommand request, CancellationToken ct)
    {
        var callerId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);

        if (request.OwnerEmployeeId is null)
        {
            // Company/department-level objective — requires OKR cycle administration rights.
            if (!_permissionChecker.HasPermission(Domain.Identity.Permissions.OkrCycleWrite))
                throw new ForbiddenException("Only an OKR administrator can create a company/department-level objective.");
        }
        else if (!await OwnershipHelper.IsSelfOrDirectManagerAsync(_db, request.OwnerEmployeeId.Value, callerId, ct))
        {
            throw new ForbiddenException("You can only create objectives for yourself or your direct reports.");
        }

        var objective = new Objective
        {
            TenantId = _tenant.TenantId,
            CycleId = request.CycleId,
            OwnerEmployeeId = request.OwnerEmployeeId,
            OwnerDepartmentId = request.OwnerDepartmentId,
            Title = request.Title,
            ParentObjectiveId = request.ParentObjectiveId
        };
        _db.Objectives.Add(objective);
        await _db.SaveChangesAsync(ct);
        return objective.Id;
    }
}

public class UpdateObjectiveCommandHandler : IRequestHandler<UpdateObjectiveCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly IPermissionChecker _permissionChecker;
    public UpdateObjectiveCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver, IPermissionChecker permissionChecker)
    { _db = db; _employeeResolver = employeeResolver; _permissionChecker = permissionChecker; }

    public async Task Handle(UpdateObjectiveCommand request, CancellationToken ct)
    {
        var objective = await _db.Objectives.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Objective), request.Id);
        await EnsureCanManageAsync(_db, _employeeResolver, _permissionChecker, objective, ct);

        objective.Title = request.Title;
        objective.ParentObjectiveId = request.ParentObjectiveId;
        await _db.SaveChangesAsync(ct);
    }

    internal static async Task EnsureCanManageAsync(AppDbContext db, ICurrentEmployeeResolver employeeResolver, IPermissionChecker permissionChecker, Objective objective, CancellationToken ct)
    {
        var callerId = await employeeResolver.GetCurrentEmployeeIdAsync(ct);
        if (objective.OwnerEmployeeId is null)
        {
            if (!permissionChecker.HasPermission(Domain.Identity.Permissions.OkrCycleWrite))
                throw new ForbiddenException("Only an OKR administrator can manage a company/department-level objective.");
        }
        else if (!await OwnershipHelper.IsSelfOrDirectManagerAsync(db, objective.OwnerEmployeeId.Value, callerId, ct))
        {
            throw new ForbiddenException("You can only manage your own objectives or your direct reports' objectives.");
        }
    }
}

public class DeleteObjectiveCommandHandler : IRequestHandler<DeleteObjectiveCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly IPermissionChecker _permissionChecker;
    public DeleteObjectiveCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver, IPermissionChecker permissionChecker)
    { _db = db; _employeeResolver = employeeResolver; _permissionChecker = permissionChecker; }

    public async Task Handle(DeleteObjectiveCommand request, CancellationToken ct)
    {
        var objective = await _db.Objectives.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Objective), request.Id);
        await UpdateObjectiveCommandHandler.EnsureCanManageAsync(_db, _employeeResolver, _permissionChecker, objective, ct);

        var keyResults = await _db.KeyResults.Where(k => k.ObjectiveId == request.Id).ToListAsync(ct);
        _db.KeyResults.RemoveRange(keyResults);
        objective.IsDeleted = true;
        objective.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetObjectivesQueryHandler : IRequestHandler<GetObjectivesQuery, IReadOnlyList<ObjectiveDto>>
{
    private readonly AppDbContext _db;
    public GetObjectivesQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ObjectiveDto>> Handle(GetObjectivesQuery request, CancellationToken ct)
    {
        // Objectives are read broadly (top-down alignment is meant to be visible tenant-wide, per §I) —
        // only mutation is ownership-scoped.
        var query = _db.Objectives.AsQueryable();
        if (request.CycleId is not null) query = query.Where(o => o.CycleId == request.CycleId);
        if (request.OwnerEmployeeId is not null) query = query.Where(o => o.OwnerEmployeeId == request.OwnerEmployeeId);

        var objectives = await query.ToListAsync(ct);
        var objectiveIds = objectives.Select(o => o.Id).ToList();
        var keyResults = await _db.KeyResults.Where(k => objectiveIds.Contains(k.ObjectiveId)).ToListAsync(ct);

        return objectives.Select(o => new ObjectiveDto(
            o.Id, o.CycleId, o.OwnerEmployeeId, o.OwnerDepartmentId, o.Title, o.ParentObjectiveId,
            keyResults.Where(k => k.ObjectiveId == o.Id)
                .Select(k => new KeyResultDto(k.Id, k.ObjectiveId, k.Title, k.MetricType, k.StartValue, k.TargetValue, k.CurrentValue))
                .ToList())).ToList();
    }
}

public class CreateKeyResultCommandHandler : IRequestHandler<CreateKeyResultCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly IPermissionChecker _permissionChecker;
    public CreateKeyResultCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver, IPermissionChecker permissionChecker)
    { _db = db; _employeeResolver = employeeResolver; _permissionChecker = permissionChecker; }

    public async Task<Guid> Handle(CreateKeyResultCommand request, CancellationToken ct)
    {
        var objective = await _db.Objectives.FindAsync(new object[] { request.ObjectiveId }, ct) ?? throw new NotFoundException(nameof(Objective), request.ObjectiveId);
        await UpdateObjectiveCommandHandler.EnsureCanManageAsync(_db, _employeeResolver, _permissionChecker, objective, ct);

        var keyResult = new KeyResult
        {
            ObjectiveId = request.ObjectiveId,
            Title = request.Title,
            MetricType = request.MetricType,
            StartValue = request.StartValue,
            TargetValue = request.TargetValue,
            CurrentValue = request.StartValue
        };
        _db.KeyResults.Add(keyResult);
        await _db.SaveChangesAsync(ct);
        return keyResult.Id;
    }
}

public class UpdateKeyResultProgressCommandHandler : IRequestHandler<UpdateKeyResultProgressCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly IPermissionChecker _permissionChecker;
    public UpdateKeyResultProgressCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver, IPermissionChecker permissionChecker)
    { _db = db; _employeeResolver = employeeResolver; _permissionChecker = permissionChecker; }

    public async Task Handle(UpdateKeyResultProgressCommand request, CancellationToken ct)
    {
        var keyResult = await _db.KeyResults.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(KeyResult), request.Id);
        var objective = await _db.Objectives.FindAsync(new object[] { keyResult.ObjectiveId }, ct) ?? throw new NotFoundException(nameof(Objective), keyResult.ObjectiveId);
        await UpdateObjectiveCommandHandler.EnsureCanManageAsync(_db, _employeeResolver, _permissionChecker, objective, ct);

        keyResult.CurrentValue = request.CurrentValue;
        await _db.SaveChangesAsync(ct);
    }
}

public class DeleteKeyResultCommandHandler : IRequestHandler<DeleteKeyResultCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly IPermissionChecker _permissionChecker;
    public DeleteKeyResultCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver, IPermissionChecker permissionChecker)
    { _db = db; _employeeResolver = employeeResolver; _permissionChecker = permissionChecker; }

    public async Task Handle(DeleteKeyResultCommand request, CancellationToken ct)
    {
        var keyResult = await _db.KeyResults.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(KeyResult), request.Id);
        var objective = await _db.Objectives.FindAsync(new object[] { keyResult.ObjectiveId }, ct) ?? throw new NotFoundException(nameof(Objective), keyResult.ObjectiveId);
        await UpdateObjectiveCommandHandler.EnsureCanManageAsync(_db, _employeeResolver, _permissionChecker, objective, ct);

        _db.KeyResults.Remove(keyResult);
        await _db.SaveChangesAsync(ct);
    }
}

public class CreateFeedbackNoteCommandHandler : IRequestHandler<CreateFeedbackNoteCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public CreateFeedbackNoteCommandHandler(AppDbContext db, ITenantContext tenant, ICurrentEmployeeResolver employeeResolver)
    { _db = db; _tenant = tenant; _employeeResolver = employeeResolver; }

    public async Task<Guid> Handle(CreateFeedbackNoteCommand request, CancellationToken ct)
    {
        var callerId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var note = new FeedbackNote
        {
            TenantId = _tenant.TenantId,
            FromEmployeeId = callerId,
            ToEmployeeId = request.ToEmployeeId,
            Message = request.Message,
            Visibility = request.Visibility
        };
        _db.FeedbackNotes.Add(note);
        await _db.SaveChangesAsync(ct);
        return note.Id;
    }
}

public class GetFeedbackForEmployeeQueryHandler : IRequestHandler<GetFeedbackForEmployeeQuery, IReadOnlyList<FeedbackNoteDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public GetFeedbackForEmployeeQueryHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task<IReadOnlyList<FeedbackNoteDto>> Handle(GetFeedbackForEmployeeQuery request, CancellationToken ct)
    {
        var callerId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var isSelf = request.EmployeeId == callerId;
        var isDirectManager = !isSelf && await OwnershipHelper.IsSelfOrDirectManagerAsync(_db, request.EmployeeId, callerId, ct);

        var notes = await _db.FeedbackNotes.Where(n => n.ToEmployeeId == request.EmployeeId).ToListAsync(ct);
        return notes
            .Where(n => n.Visibility == FeedbackVisibility.Public || isSelf || isDirectManager || n.FromEmployeeId == callerId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Select(n => new FeedbackNoteDto(n.Id, n.FromEmployeeId, n.ToEmployeeId, n.Message, n.Visibility, n.CreatedAtUtc))
            .ToList();
    }
}
