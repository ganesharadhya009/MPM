using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.Leave;
using PeopleHQ.Domain.Auditing;
using PeopleHQ.Domain.Leave;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Leave;

// ===== Leave Types =====
public class CreateLeaveTypeCommandHandler : IRequestHandler<CreateLeaveTypeCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant; private readonly IAuditLogWriter _audit;
    public CreateLeaveTypeCommandHandler(AppDbContext db, ITenantContext tenant, IAuditLogWriter audit) { _db = db; _tenant = tenant; _audit = audit; }

    public async Task<Guid> Handle(CreateLeaveTypeCommand request, CancellationToken ct)
    {
        var leaveType = new LeaveType
        {
            TenantId = _tenant.TenantId, Name = request.Name, AccrualType = request.AccrualType,
            AnnualEntitlement = request.AnnualEntitlement, CarryForwardCap = request.CarryForwardCap, RequiresDocumentAfterDays = request.RequiresDocumentAfterDays
        };
        _db.LeaveTypes.Add(leaveType);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(LeaveType), leaveType.Id, AuditAction.Create, null, leaveType, ct);
        return leaveType.Id;
    }
}

public class UpdateLeaveTypeCommandHandler : IRequestHandler<UpdateLeaveTypeCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public UpdateLeaveTypeCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(UpdateLeaveTypeCommand request, CancellationToken ct)
    {
        var leaveType = await _db.LeaveTypes.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(LeaveType), request.Id);
        var before = new { leaveType.Name, leaveType.AccrualType, leaveType.AnnualEntitlement, leaveType.CarryForwardCap, leaveType.RequiresDocumentAfterDays };
        leaveType.Name = request.Name; leaveType.AccrualType = request.AccrualType; leaveType.AnnualEntitlement = request.AnnualEntitlement;
        leaveType.CarryForwardCap = request.CarryForwardCap; leaveType.RequiresDocumentAfterDays = request.RequiresDocumentAfterDays;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(LeaveType), leaveType.Id, AuditAction.Update, before, leaveType, ct);
    }
}

public class DeleteLeaveTypeCommandHandler : IRequestHandler<DeleteLeaveTypeCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public DeleteLeaveTypeCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(DeleteLeaveTypeCommand request, CancellationToken ct)
    {
        var leaveType = await _db.LeaveTypes.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(LeaveType), request.Id);
        var hasRequests = await _db.LeaveRequests.AnyAsync(r => r.LeaveTypeId == request.Id, ct);
        if (hasRequests) throw new ConflictException($"Leave type '{leaveType.Name}' has existing leave requests and cannot be deleted.");

        leaveType.IsDeleted = true; leaveType.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(LeaveType), leaveType.Id, AuditAction.Delete, leaveType, null, ct);
    }
}

public class GetLeaveTypesQueryHandler : IRequestHandler<GetLeaveTypesQuery, IReadOnlyList<LeaveTypeDto>>
{
    private readonly AppDbContext _db;
    public GetLeaveTypesQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<LeaveTypeDto>> Handle(GetLeaveTypesQuery request, CancellationToken ct)
        => await _db.LeaveTypes.OrderBy(t => t.Name)
            .Select(t => new LeaveTypeDto(t.Id, t.Name, t.AccrualType, t.AnnualEntitlement, t.CarryForwardCap, t.RequiresDocumentAfterDays))
            .ToListAsync(ct);
}

// ===== Leave Policies =====
public class CreateLeavePolicyCommandHandler : IRequestHandler<CreateLeavePolicyCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant;
    public CreateLeavePolicyCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<Guid> Handle(CreateLeavePolicyCommand request, CancellationToken ct)
    {
        var policy = new LeavePolicy { TenantId = _tenant.TenantId, Name = request.Name, AppliesToJson = request.AppliesToJson };
        _db.LeavePolicies.Add(policy);
        await _db.SaveChangesAsync(ct); // need policy.Id for rules

        foreach (var rule in request.Rules)
            _db.LeaveTypePolicyRules.Add(new LeaveTypePolicyRule { PolicyId = policy.Id, LeaveTypeId = rule.LeaveTypeId, EntitlementOverride = rule.EntitlementOverride });
        await _db.SaveChangesAsync(ct);
        return policy.Id;
    }
}

public class UpdateLeavePolicyCommandHandler : IRequestHandler<UpdateLeavePolicyCommand>
{
    private readonly AppDbContext _db;
    public UpdateLeavePolicyCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(UpdateLeavePolicyCommand request, CancellationToken ct)
    {
        var policy = await _db.LeavePolicies.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(LeavePolicy), request.Id);
        policy.Name = request.Name; policy.AppliesToJson = request.AppliesToJson;

        var existingRules = await _db.LeaveTypePolicyRules.Where(r => r.PolicyId == policy.Id).ToListAsync(ct);
        _db.LeaveTypePolicyRules.RemoveRange(existingRules);
        foreach (var rule in request.Rules)
            _db.LeaveTypePolicyRules.Add(new LeaveTypePolicyRule { PolicyId = policy.Id, LeaveTypeId = rule.LeaveTypeId, EntitlementOverride = rule.EntitlementOverride });

        await _db.SaveChangesAsync(ct);
    }
}

public class DeleteLeavePolicyCommandHandler : IRequestHandler<DeleteLeavePolicyCommand>
{
    private readonly AppDbContext _db;
    public DeleteLeavePolicyCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(DeleteLeavePolicyCommand request, CancellationToken ct)
    {
        var policy = await _db.LeavePolicies.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(LeavePolicy), request.Id);
        var rules = await _db.LeaveTypePolicyRules.Where(r => r.PolicyId == policy.Id).ToListAsync(ct);
        _db.LeaveTypePolicyRules.RemoveRange(rules);
        policy.IsDeleted = true; policy.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetLeavePoliciesQueryHandler : IRequestHandler<GetLeavePoliciesQuery, IReadOnlyList<LeavePolicyDto>>
{
    private readonly AppDbContext _db;
    public GetLeavePoliciesQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<LeavePolicyDto>> Handle(GetLeavePoliciesQuery request, CancellationToken ct)
    {
        var policies = await _db.LeavePolicies.OrderBy(p => p.Name).ToListAsync(ct);
        var policyIds = policies.Select(p => p.Id).ToList();
        var rulesByPolicy = (await _db.LeaveTypePolicyRules.Where(r => policyIds.Contains(r.PolicyId)).ToListAsync(ct)).ToLookup(r => r.PolicyId);

        return policies.Select(p => new LeavePolicyDto(p.Id, p.Name, p.AppliesToJson,
            rulesByPolicy[p.Id].Select(r => new LeavePolicyRuleDto(r.LeaveTypeId, r.EntitlementOverride)).ToList())).ToList();
    }
}

public class AssignLeavePolicyCommandHandler : IRequestHandler<AssignLeavePolicyCommand>
{
    private readonly AppDbContext _db;
    public AssignLeavePolicyCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(AssignLeavePolicyCommand request, CancellationToken ct)
    {
        var existing = await _db.EmployeeLeavePolicies.Where(e => e.EmployeeId == request.EmployeeId).ToListAsync(ct);
        _db.EmployeeLeavePolicies.RemoveRange(existing); // one active policy per employee (simplest v1 model)
        _db.EmployeeLeavePolicies.Add(new EmployeeLeavePolicy { EmployeeId = request.EmployeeId, PolicyId = request.PolicyId });
        await _db.SaveChangesAsync(ct);
    }
}

// ===== Balances =====
public class GetLeaveBalancesQueryHandler : IRequestHandler<GetLeaveBalancesQuery, IReadOnlyList<LeaveBalanceDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly IPermissionChecker _permissionChecker;
    public GetLeaveBalancesQueryHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver, IPermissionChecker permissionChecker)
    { _db = db; _employeeResolver = employeeResolver; _permissionChecker = permissionChecker; }

    public async Task<IReadOnlyList<LeaveBalanceDto>> Handle(GetLeaveBalancesQuery request, CancellationToken ct)
    {
        // leave.read is also granted to the plain Employee role for self-service; without this check any
        // authenticated employee could pass another employee's id and read their balance (IDOR).
        var callerEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        if (request.EmployeeId != callerEmployeeId && !_permissionChecker.HasPermission(Domain.Identity.Permissions.LeaveApprove))
            throw new ForbiddenException("You can only view your own leave balance.");

        var leaveTypes = await _db.LeaveTypes.ToListAsync(ct);
        var balances = await _db.LeaveBalances.Where(b => b.EmployeeId == request.EmployeeId && b.Year == request.Year).ToListAsync(ct);
        var balanceByType = balances.ToDictionary(b => b.LeaveTypeId);

        return leaveTypes.Select(t =>
        {
            balanceByType.TryGetValue(t.Id, out var balance);
            var accrued = balance?.Accrued ?? 0m;
            var used = balance?.Used ?? 0m;
            var carriedForward = balance?.CarriedForward ?? 0m;
            var reserved = balance?.Reserved ?? 0m;
            return new LeaveBalanceDto(t.Id, request.Year, accrued, used, carriedForward, reserved, accrued + carriedForward - used - reserved);
        }).ToList();
    }
}

// ===== Leave Requests =====
public class ApplyLeaveCommandHandler : IRequestHandler<ApplyLeaveCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly Application.Workflow.IWorkflowEngine _workflowEngine;

    public ApplyLeaveCommandHandler(AppDbContext db, ITenantContext tenant, ICurrentEmployeeResolver employeeResolver, Application.Workflow.IWorkflowEngine workflowEngine)
    { _db = db; _tenant = tenant; _employeeResolver = employeeResolver; _workflowEngine = workflowEngine; }

    public async Task<Guid> Handle(ApplyLeaveCommand request, CancellationToken ct)
    {
        if (request.EndDate < request.StartDate) throw new ValidationException(nameof(request.EndDate), "End date must be on or after the start date.");

        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var days = LeaveDayCalculator.CalculateDays(request.StartDate, request.EndDate, request.IsHalfDay);
        var year = request.StartDate.Year;

        var isBlackedOut = await _db.LeaveBlackoutPeriods.AnyAsync(b =>
            b.IsBlocking && b.StartDate <= request.EndDate && b.EndDate >= request.StartDate, ct);
        if (isBlackedOut) throw new ConflictException("The requested dates fall within a blackout period.");

        var balance = await _db.LeaveBalances.FirstOrDefaultAsync(b => b.EmployeeId == employeeId && b.LeaveTypeId == request.LeaveTypeId && b.Year == year, ct);
        if (balance is null)
        {
            balance = new LeaveBalance { EmployeeId = employeeId, LeaveTypeId = request.LeaveTypeId, Year = year };
            _db.LeaveBalances.Add(balance);
        }
        var available = balance.Accrued + balance.CarriedForward - balance.Used - balance.Reserved;
        if (days > available) throw new ConflictException($"Insufficient leave balance: {available} available, {days} requested.");

        var leaveRequest = new LeaveRequest
        {
            TenantId = _tenant.TenantId, EmployeeId = employeeId, LeaveTypeId = request.LeaveTypeId,
            StartDate = request.StartDate, EndDate = request.EndDate, IsHalfDay = request.IsHalfDay,
            Reason = request.Reason, AttachmentBlobUrl = request.AttachmentBlobUrl, Status = LeaveRequestStatus.Pending
        };
        _db.LeaveRequests.Add(leaveRequest);
        balance.Reserved += days; // FR-LVE-06: provisional hold, released on reject/withdraw, finalized on approval
        await _db.SaveChangesAsync(ct); // need leaveRequest.Id for the workflow payload

        var workflowRequestId = await _workflowEngine.SubmitAsync(
            Domain.Workflow.WorkflowRequestType.LeaveRequest, employeeId,
            new { leaveRequest.Id, leaveRequest.LeaveTypeId, leaveRequest.StartDate, leaveRequest.EndDate, Days = days }, ct);

        leaveRequest.WorkflowRequestId = workflowRequestId;
        await _db.SaveChangesAsync(ct);
        return leaveRequest.Id;
    }
}

public class GetLeaveRequestsQueryHandler : IRequestHandler<GetLeaveRequestsQuery, IReadOnlyList<LeaveRequestDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly IPermissionChecker _permissionChecker;
    public GetLeaveRequestsQueryHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver, IPermissionChecker permissionChecker)
    { _db = db; _employeeResolver = employeeResolver; _permissionChecker = permissionChecker; }

    public async Task<IReadOnlyList<LeaveRequestDto>> Handle(GetLeaveRequestsQuery request, CancellationToken ct)
    {
        // leave.read is also granted to the plain Employee role for self-service; without this check any
        // authenticated employee could pass another employee's id (or omit it to see everyone) and read
        // others' leave history (IDOR). A caller without leave.approve is always scoped to their own records;
        // one with it may query any employee or, with EmployeeId omitted, every employee's requests.
        var callerEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var canViewOthers = _permissionChecker.HasPermission(Domain.Identity.Permissions.LeaveApprove);
        if (!canViewOthers && request.EmployeeId is not null && request.EmployeeId != callerEmployeeId)
            throw new ForbiddenException("You can only view your own leave requests.");

        var effectiveEmployeeId = canViewOthers ? request.EmployeeId : callerEmployeeId;
        var query = _db.LeaveRequests.AsQueryable();
        if (effectiveEmployeeId is not null) query = query.Where(r => r.EmployeeId == effectiveEmployeeId);
        if (request.Status is not null) query = query.Where(r => r.Status == request.Status);

        return await query.OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new LeaveRequestDto(r.Id, r.EmployeeId, r.LeaveTypeId, r.StartDate, r.EndDate, r.IsHalfDay, r.Reason, r.Status, r.WorkflowRequestId))
            .ToListAsync(ct);
    }
}

public class GetTeamLeaveCalendarQueryHandler : IRequestHandler<GetTeamLeaveCalendarQuery, IReadOnlyList<TeamLeaveCalendarEntryDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly IPermissionChecker _permissionChecker;
    public GetTeamLeaveCalendarQueryHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver, IPermissionChecker permissionChecker)
    { _db = db; _employeeResolver = employeeResolver; _permissionChecker = permissionChecker; }

    public async Task<IReadOnlyList<TeamLeaveCalendarEntryDto>> Handle(GetTeamLeaveCalendarQuery request, CancellationToken ct)
    {
        // A caller may always view their own team; viewing another manager's team needs leave.approve
        // (proxy for HR/admin scope) — otherwise any employee holding leave.read could enumerate any team's leave.
        var callerEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        if (request.ManagerId != callerEmployeeId && !_permissionChecker.HasPermission(Domain.Identity.Permissions.LeaveApprove))
            throw new ForbiddenException("You can only view your own team's leave calendar.");

        var reporteeIds = await _db.Employees.Where(e => e.ManagerId == request.ManagerId).Select(e => e.Id).ToListAsync(ct);
        return await _db.LeaveRequests
            .Where(r => reporteeIds.Contains(r.EmployeeId)
                     && (r.Status == LeaveRequestStatus.Approved || r.Status == LeaveRequestStatus.Pending)
                     && r.StartDate <= request.To && r.EndDate >= request.From)
            .Select(r => new TeamLeaveCalendarEntryDto(r.EmployeeId, r.Id, r.LeaveTypeId, r.StartDate, r.EndDate, r.Status))
            .ToListAsync(ct);
    }
}

public class GetBradfordScoreQueryHandler : IRequestHandler<GetBradfordScoreQuery, BradfordScoreDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly IPermissionChecker _permissionChecker;
    public GetBradfordScoreQueryHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver, IPermissionChecker permissionChecker)
    { _db = db; _employeeResolver = employeeResolver; _permissionChecker = permissionChecker; }

    public async Task<BradfordScoreDto> Handle(GetBradfordScoreQuery request, CancellationToken ct)
    {
        var callerEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        if (request.EmployeeId != callerEmployeeId && !_permissionChecker.HasPermission(Domain.Identity.Permissions.LeaveApprove))
            throw new ForbiddenException("You can only view your own absence score.");

        var approvedRequests = await _db.LeaveRequests
            .Where(r => r.EmployeeId == request.EmployeeId && r.Status == LeaveRequestStatus.Approved && r.StartDate.Year == request.Year)
            .ToListAsync(ct);

        var spells = approvedRequests.Count;
        var totalDays = approvedRequests.Sum(r => LeaveDayCalculator.CalculateDays(r.StartDate, r.EndDate, r.IsHalfDay));
        var score = spells * spells * totalDays;
        return new BradfordScoreDto(request.EmployeeId, request.Year, spells, totalDays, score);
    }
}

// ===== Blackout Periods =====
public class CreateLeaveBlackoutPeriodCommandHandler : IRequestHandler<CreateLeaveBlackoutPeriodCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant;
    public CreateLeaveBlackoutPeriodCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<Guid> Handle(CreateLeaveBlackoutPeriodCommand request, CancellationToken ct)
    {
        var period = new LeaveBlackoutPeriod { TenantId = _tenant.TenantId, Name = request.Name, StartDate = request.StartDate, EndDate = request.EndDate, IsBlocking = request.IsBlocking };
        _db.LeaveBlackoutPeriods.Add(period);
        await _db.SaveChangesAsync(ct);
        return period.Id;
    }
}

public class DeleteLeaveBlackoutPeriodCommandHandler : IRequestHandler<DeleteLeaveBlackoutPeriodCommand>
{
    private readonly AppDbContext _db;
    public DeleteLeaveBlackoutPeriodCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(DeleteLeaveBlackoutPeriodCommand request, CancellationToken ct)
    {
        var period = await _db.LeaveBlackoutPeriods.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(LeaveBlackoutPeriod), request.Id);
        period.IsDeleted = true; period.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetLeaveBlackoutPeriodsQueryHandler : IRequestHandler<GetLeaveBlackoutPeriodsQuery, IReadOnlyList<LeaveBlackoutPeriodDto>>
{
    private readonly AppDbContext _db;
    public GetLeaveBlackoutPeriodsQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<LeaveBlackoutPeriodDto>> Handle(GetLeaveBlackoutPeriodsQuery request, CancellationToken ct)
        => await _db.LeaveBlackoutPeriods.OrderBy(b => b.StartDate)
            .Select(b => new LeaveBlackoutPeriodDto(b.Id, b.Name, b.StartDate, b.EndDate, b.IsBlocking)).ToListAsync(ct);
}

/// <summary>Finalizes or releases the Reserved balance hold once a LeaveRequest's WorkflowRequest resolves
/// (FR-LVE-06) — the module-owned side effect the generic engine deliberately doesn't know about.</summary>
public class LeaveRequestResolvedHandler : INotificationHandler<Application.Workflow.WorkflowRequestResolvedNotification>
{
    private readonly AppDbContext _db;
    public LeaveRequestResolvedHandler(AppDbContext db) => _db = db;

    public async Task Handle(Application.Workflow.WorkflowRequestResolvedNotification notification, CancellationToken ct)
    {
        if (notification.RequestType != Domain.Workflow.WorkflowRequestType.LeaveRequest) return;

        var leaveRequest = await _db.LeaveRequests.FirstOrDefaultAsync(r => r.WorkflowRequestId == notification.WorkflowRequestId, ct);
        if (leaveRequest is null) return;

        var days = LeaveDayCalculator.CalculateDays(leaveRequest.StartDate, leaveRequest.EndDate, leaveRequest.IsHalfDay);
        var balance = await _db.LeaveBalances.FirstOrDefaultAsync(b =>
            b.EmployeeId == leaveRequest.EmployeeId && b.LeaveTypeId == leaveRequest.LeaveTypeId && b.Year == leaveRequest.StartDate.Year, ct);

        if (notification.Status == Domain.Workflow.WorkflowStatus.Approved)
        {
            leaveRequest.Status = LeaveRequestStatus.Approved;
            if (balance is not null) { balance.Reserved -= days; balance.Used += days; }
        }
        else if (notification.Status == Domain.Workflow.WorkflowStatus.Rejected)
        {
            leaveRequest.Status = LeaveRequestStatus.Rejected;
            if (balance is not null) balance.Reserved -= days;
        }
        else if (notification.Status == Domain.Workflow.WorkflowStatus.Withdrawn)
        {
            leaveRequest.Status = LeaveRequestStatus.Withdrawn;
            if (balance is not null) balance.Reserved -= days;
        }

        await _db.SaveChangesAsync(ct);
    }
}
