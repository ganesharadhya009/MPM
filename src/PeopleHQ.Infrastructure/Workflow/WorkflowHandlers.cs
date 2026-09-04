using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.Workflow;
using PeopleHQ.Domain.Workflow;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Workflow;

public class GetMyPendingApprovalsQueryHandler : IRequestHandler<GetMyPendingApprovalsQuery, IReadOnlyList<PendingApprovalDto>>
{
    private readonly AppDbContext _db; private readonly ICurrentEmployeeResolver _employeeResolver;
    public GetMyPendingApprovalsQueryHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task<IReadOnlyList<PendingApprovalDto>> Handle(GetMyPendingApprovalsQuery request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var delegatedFromIds = await _db.Delegations
            .Where(d => d.ToEmployeeId == employeeId && d.StartDate <= today && d.EndDate >= today)
            .Select(d => d.FromEmployeeId).ToListAsync(ct);
        var approverIds = delegatedFromIds.Append(employeeId).ToList();

        return await (
            from wr in _db.WorkflowRequests
            join step in _db.WorkflowApprovalSteps on wr.Id equals step.WorkflowRequestId
            where wr.Status == WorkflowStatus.Pending && step.StepOrder == wr.CurrentStepOrder
                  && approverIds.Contains(step.ApproverEmployeeId) && step.Status == ApprovalStepStatus.Pending
            select new PendingApprovalDto(wr.Id, wr.RequestType, wr.RequesterEmployeeId, wr.PayloadJson, step.StepOrder, wr.SubmittedAtUtc)
        ).ToListAsync(ct);
    }
}

public class GetMyRequestsQueryHandler : IRequestHandler<GetMyRequestsQuery, IReadOnlyList<MyRequestDto>>
{
    private readonly AppDbContext _db; private readonly ICurrentEmployeeResolver _employeeResolver;
    public GetMyRequestsQueryHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task<IReadOnlyList<MyRequestDto>> Handle(GetMyRequestsQuery request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var query = _db.WorkflowRequests.Where(r => r.RequesterEmployeeId == employeeId);
        if (request.Status is not null) query = query.Where(r => r.Status == request.Status);

        return await query.OrderByDescending(r => r.SubmittedAtUtc)
            .Select(r => new MyRequestDto(r.Id, r.RequestType, r.PayloadJson, r.Status, r.CurrentStepOrder, r.SubmittedAtUtc, r.ResolvedAtUtc))
            .ToListAsync(ct);
    }
}

public class ApproveWorkflowRequestCommandHandler : IRequestHandler<ApproveWorkflowRequestCommand>
{
    private readonly IWorkflowEngine _engine; private readonly ICurrentEmployeeResolver _employeeResolver;
    public ApproveWorkflowRequestCommandHandler(IWorkflowEngine engine, ICurrentEmployeeResolver employeeResolver) { _engine = engine; _employeeResolver = employeeResolver; }

    public async Task Handle(ApproveWorkflowRequestCommand request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        await _engine.ApproveCurrentStepAsync(request.WorkflowRequestId, employeeId, request.Comment, ct);
    }
}

public class RejectWorkflowRequestCommandHandler : IRequestHandler<RejectWorkflowRequestCommand>
{
    private readonly IWorkflowEngine _engine; private readonly ICurrentEmployeeResolver _employeeResolver;
    public RejectWorkflowRequestCommandHandler(IWorkflowEngine engine, ICurrentEmployeeResolver employeeResolver) { _engine = engine; _employeeResolver = employeeResolver; }

    public async Task Handle(RejectWorkflowRequestCommand request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        await _engine.RejectCurrentStepAsync(request.WorkflowRequestId, employeeId, request.Comment, ct);
    }
}

public class WithdrawWorkflowRequestCommandHandler : IRequestHandler<WithdrawWorkflowRequestCommand>
{
    private readonly IWorkflowEngine _engine; private readonly ICurrentEmployeeResolver _employeeResolver;
    public WithdrawWorkflowRequestCommandHandler(IWorkflowEngine engine, ICurrentEmployeeResolver employeeResolver) { _engine = engine; _employeeResolver = employeeResolver; }

    public async Task Handle(WithdrawWorkflowRequestCommand request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        await _engine.WithdrawAsync(request.WorkflowRequestId, employeeId, ct);
    }
}

// ===== Chain Rules =====
public class CreateWorkflowChainRuleCommandHandler : IRequestHandler<CreateWorkflowChainRuleCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant;
    public CreateWorkflowChainRuleCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<Guid> Handle(CreateWorkflowChainRuleCommand request, CancellationToken ct)
    {
        var rule = new WorkflowChainRule { TenantId = _tenant.TenantId, RequestType = request.RequestType, RuleJson = request.RuleJson, Order = request.Order };
        _db.WorkflowChainRules.Add(rule);
        await _db.SaveChangesAsync(ct);
        return rule.Id;
    }
}

public class DeleteWorkflowChainRuleCommandHandler : IRequestHandler<DeleteWorkflowChainRuleCommand>
{
    private readonly AppDbContext _db;
    public DeleteWorkflowChainRuleCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(DeleteWorkflowChainRuleCommand request, CancellationToken ct)
    {
        var rule = await _db.WorkflowChainRules.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(WorkflowChainRule), request.Id);
        rule.IsDeleted = true; rule.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetWorkflowChainRulesQueryHandler : IRequestHandler<GetWorkflowChainRulesQuery, IReadOnlyList<WorkflowChainRuleDto>>
{
    private readonly AppDbContext _db;
    public GetWorkflowChainRulesQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<WorkflowChainRuleDto>> Handle(GetWorkflowChainRulesQuery request, CancellationToken ct)
        => await _db.WorkflowChainRules.Where(r => r.RequestType == request.RequestType).OrderBy(r => r.Order)
            .Select(r => new WorkflowChainRuleDto(r.Id, r.RequestType, r.RuleJson, r.Order)).ToListAsync(ct);
}

// ===== Delegation =====
public class CreateDelegationCommandHandler : IRequestHandler<CreateDelegationCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant; private readonly ICurrentEmployeeResolver _employeeResolver;
    public CreateDelegationCommandHandler(AppDbContext db, ITenantContext tenant, ICurrentEmployeeResolver employeeResolver) { _db = db; _tenant = tenant; _employeeResolver = employeeResolver; }

    public async Task<Guid> Handle(CreateDelegationCommand request, CancellationToken ct)
    {
        var fromEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        if (request.EndDate < request.StartDate) throw new ValidationException(nameof(request.EndDate), "End date must be on or after the start date.");

        var delegation = new Domain.Workflow.Delegation
        {
            TenantId = _tenant.TenantId, FromEmployeeId = fromEmployeeId, ToEmployeeId = request.ToEmployeeId,
            StartDate = request.StartDate, EndDate = request.EndDate
        };
        _db.Delegations.Add(delegation);
        await _db.SaveChangesAsync(ct);
        return delegation.Id;
    }
}

public class DeleteDelegationCommandHandler : IRequestHandler<DeleteDelegationCommand>
{
    private readonly AppDbContext _db; private readonly ICurrentEmployeeResolver _employeeResolver;
    public DeleteDelegationCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task Handle(DeleteDelegationCommand request, CancellationToken ct)
    {
        var delegation = await _db.Delegations.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Domain.Workflow.Delegation), request.Id);
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        if (delegation.FromEmployeeId != employeeId) throw new ForbiddenException("Only the delegator can revoke this delegation.");

        delegation.IsDeleted = true; delegation.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetMyDelegationsQueryHandler : IRequestHandler<GetMyDelegationsQuery, IReadOnlyList<DelegationDto>>
{
    private readonly AppDbContext _db; private readonly ICurrentEmployeeResolver _employeeResolver;
    public GetMyDelegationsQueryHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task<IReadOnlyList<DelegationDto>> Handle(GetMyDelegationsQuery request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        return await _db.Delegations.Where(d => d.FromEmployeeId == employeeId)
            .Select(d => new DelegationDto(d.Id, d.FromEmployeeId, d.ToEmployeeId, d.StartDate, d.EndDate)).ToListAsync(ct);
    }
}
