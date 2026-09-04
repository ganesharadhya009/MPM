using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.Workflow;
using PeopleHQ.Domain.Employees;
using PeopleHQ.Domain.OrgStructure;
using PeopleHQ.Domain.Workflow;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Workflow;

/// <summary>
/// Resolves the approver chain from WorkflowChainRule (falling back to a single direct-manager step when no
/// rule is configured for the request type), creates the WorkflowRequest + WorkflowApprovalStep rows at
/// submission time, and drives sequential approve/reject/withdraw transitions. v1 supports Sequential steps only
/// (AnyOf/AllOf are modeled in the domain for future multi-approver-per-step support, not yet implemented here).
/// </summary>
public class WorkflowEngine : IWorkflowEngine
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public WorkflowEngine(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<Guid> SubmitAsync(WorkflowRequestType requestType, Guid requesterEmployeeId, object payload, CancellationToken ct = default)
    {
        var chain = await ResolveApproverChainAsync(requestType, requesterEmployeeId, ct);

        var request = new WorkflowRequest
        {
            TenantId = _tenant.TenantId,
            RequestType = requestType,
            RequesterEmployeeId = requesterEmployeeId,
            PayloadJson = JsonSerializer.Serialize(payload),
            Status = WorkflowStatus.Pending,
            CurrentStepOrder = 1,
            SubmittedAtUtc = DateTime.UtcNow
        };
        _db.WorkflowRequests.Add(request);
        await _db.SaveChangesAsync(ct); // need request.Id for steps

        if (chain.Count == 0)
        {
            // No resolvable approver (e.g. requester has no manager) — auto-approve so the request never stalls.
            request.Status = WorkflowStatus.Approved;
            request.ResolvedAtUtc = DateTime.UtcNow;
        }
        else
        {
            for (var i = 0; i < chain.Count; i++)
            {
                _db.WorkflowApprovalSteps.Add(new WorkflowApprovalStep
                {
                    WorkflowRequestId = request.Id,
                    StepOrder = i + 1,
                    ApproverEmployeeId = chain[i],
                    Mode = ApprovalStepMode.Sequential,
                    Status = ApprovalStepStatus.Pending
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return request.Id;
    }

    public async Task ApproveCurrentStepAsync(Guid workflowRequestId, Guid actingEmployeeId, string? comment, CancellationToken ct = default)
    {
        var request = await _db.WorkflowRequests.FindAsync(new object[] { workflowRequestId }, ct) ?? throw new NotFoundException(nameof(WorkflowRequest), workflowRequestId);
        if (request.Status != WorkflowStatus.Pending) throw new ConflictException("This request is not pending approval.");

        var step = await GetActionableCurrentStepAsync(request, actingEmployeeId, ct);
        step.Status = ApprovalStepStatus.Approved;
        step.ActedAtUtc = DateTime.UtcNow;
        step.Comment = comment;
        if (step.ApproverEmployeeId != actingEmployeeId) step.ActedOnBehalfOfEmployeeId = step.ApproverEmployeeId;

        var nextStep = await _db.WorkflowApprovalSteps
            .Where(s => s.WorkflowRequestId == request.Id && s.StepOrder == request.CurrentStepOrder + 1)
            .FirstOrDefaultAsync(ct);

        if (nextStep is null)
        {
            request.Status = WorkflowStatus.Approved;
            request.ResolvedAtUtc = DateTime.UtcNow;
        }
        else
        {
            request.CurrentStepOrder += 1;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task RejectCurrentStepAsync(Guid workflowRequestId, Guid actingEmployeeId, string? comment, CancellationToken ct = default)
    {
        var request = await _db.WorkflowRequests.FindAsync(new object[] { workflowRequestId }, ct) ?? throw new NotFoundException(nameof(WorkflowRequest), workflowRequestId);
        if (request.Status != WorkflowStatus.Pending) throw new ConflictException("This request is not pending approval.");

        var step = await GetActionableCurrentStepAsync(request, actingEmployeeId, ct);
        step.Status = ApprovalStepStatus.Rejected;
        step.ActedAtUtc = DateTime.UtcNow;
        step.Comment = comment;
        if (step.ApproverEmployeeId != actingEmployeeId) step.ActedOnBehalfOfEmployeeId = step.ApproverEmployeeId;

        var remainingSteps = await _db.WorkflowApprovalSteps
            .Where(s => s.WorkflowRequestId == request.Id && s.StepOrder > request.CurrentStepOrder && s.Status == ApprovalStepStatus.Pending)
            .ToListAsync(ct);
        foreach (var remaining in remainingSteps) remaining.Status = ApprovalStepStatus.Skipped;

        request.Status = WorkflowStatus.Rejected;
        request.ResolvedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task WithdrawAsync(Guid workflowRequestId, Guid requesterEmployeeId, CancellationToken ct = default)
    {
        var request = await _db.WorkflowRequests.FindAsync(new object[] { workflowRequestId }, ct) ?? throw new NotFoundException(nameof(WorkflowRequest), workflowRequestId);
        if (request.RequesterEmployeeId != requesterEmployeeId) throw new ForbiddenException("Only the requester can withdraw this request.");
        if (request.Status is not (WorkflowStatus.Draft or WorkflowStatus.Pending)) throw new ConflictException("Only a draft or pending request can be withdrawn.");

        var pendingSteps = await _db.WorkflowApprovalSteps.Where(s => s.WorkflowRequestId == request.Id && s.Status == ApprovalStepStatus.Pending).ToListAsync(ct);
        foreach (var step in pendingSteps) step.Status = ApprovalStepStatus.Skipped;

        request.Status = WorkflowStatus.Withdrawn;
        request.ResolvedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Returns the current-step row if actingEmployeeId is its approver or an active delegate of that
    /// approver (FR-WF-06); throws ForbiddenException otherwise.</summary>
    private async Task<WorkflowApprovalStep> GetActionableCurrentStepAsync(WorkflowRequest request, Guid actingEmployeeId, CancellationToken ct)
    {
        var step = await _db.WorkflowApprovalSteps
            .FirstOrDefaultAsync(s => s.WorkflowRequestId == request.Id && s.StepOrder == request.CurrentStepOrder, ct)
            ?? throw new ConflictException("This request has no current approval step.");

        if (step.ApproverEmployeeId == actingEmployeeId) return step;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var isDelegate = await _db.Delegations.AnyAsync(d =>
            d.FromEmployeeId == step.ApproverEmployeeId && d.ToEmployeeId == actingEmployeeId &&
            d.StartDate <= today && d.EndDate >= today, ct);

        if (!isDelegate) throw new ForbiddenException("You are not the approver (or an active delegate) for this step.");
        return step;
    }

    private async Task<List<Guid>> ResolveApproverChainAsync(WorkflowRequestType requestType, Guid requesterEmployeeId, CancellationToken ct)
    {
        var employee = await _db.Employees.FindAsync(new object[] { requesterEmployeeId }, ct) ?? throw new NotFoundException(nameof(Employee), requesterEmployeeId);
        var rules = await _db.WorkflowChainRules.Where(r => r.RequestType == requestType).OrderBy(r => r.Order).ToListAsync(ct);

        if (rules.Count == 0)
        {
            // Default chain: single step, requester's direct manager.
            return employee.ManagerId is null ? new List<Guid>() : new List<Guid> { employee.ManagerId.Value };
        }

        var chain = new List<Guid>();
        foreach (var rule in rules)
        {
            var approverType = ParseApproverType(rule.RuleJson);
            Guid? approverId = approverType switch
            {
                "direct_manager" => employee.ManagerId,
                "department_head" => employee.DepartmentId is null ? null
                    : await _db.Departments.Where(d => d.Id == employee.DepartmentId).Select(d => d.HeadEmployeeId).FirstOrDefaultAsync(ct),
                _ => null
            };
            if (approverId is not null && approverId != Guid.Empty) chain.Add(approverId.Value);
        }
        return chain;
    }

    private static string? ParseApproverType(string ruleJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(ruleJson);
            return doc.RootElement.TryGetProperty("approver", out var prop) ? prop.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
