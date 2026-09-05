using System.Text.Json;
using MediatR;
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
/// Publishes WorkflowRequestResolvedNotification on every terminal transition so modules can apply their own
/// side effects without this engine knowing about their domains.
/// </summary>
public class WorkflowEngine : IWorkflowEngine
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPublisher _publisher;
    private readonly INotificationService _notificationService;

    public WorkflowEngine(AppDbContext db, ITenantContext tenant, IPublisher publisher, INotificationService notificationService)
    {
        _db = db;
        _tenant = tenant;
        _publisher = publisher;
        _notificationService = notificationService;
    }

    public async Task<Guid> SubmitAsync(WorkflowRequestType requestType, Guid requesterEmployeeId, object payload, CancellationToken ct = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload);
        var chain = await ResolveApproverChainAsync(requestType, requesterEmployeeId, payloadJson, ct);

        var request = new WorkflowRequest
        {
            TenantId = _tenant.TenantId,
            RequestType = requestType,
            RequesterEmployeeId = requesterEmployeeId,
            PayloadJson = payloadJson,
            Status = WorkflowStatus.Pending,
            CurrentStepOrder = 1,
            SubmittedAtUtc = DateTime.UtcNow
        };
        _db.WorkflowRequests.Add(request);
        await _db.SaveChangesAsync(ct); // need request.Id for steps

        var autoApproved = chain.Count == 0;
        if (autoApproved)
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
        if (autoApproved)
            await _publisher.Publish(new WorkflowRequestResolvedNotification(request.Id, request.RequestType, request.Status), ct);
        else
            await NotifyApproverAsync(chain[0], request.RequestType, ct);
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

        var resolved = nextStep is null;
        if (resolved)
        {
            request.Status = WorkflowStatus.Approved;
            request.ResolvedAtUtc = DateTime.UtcNow;
        }
        else
        {
            request.CurrentStepOrder += 1;
        }

        await _db.SaveChangesAsync(ct);
        if (resolved)
            await _publisher.Publish(new WorkflowRequestResolvedNotification(request.Id, request.RequestType, request.Status), ct);
        else if (nextStep is not null)
            await NotifyApproverAsync(nextStep.ApproverEmployeeId, request.RequestType, ct);
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
        await _publisher.Publish(new WorkflowRequestResolvedNotification(request.Id, request.RequestType, request.Status), ct);
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
        await _publisher.Publish(new WorkflowRequestResolvedNotification(request.Id, request.RequestType, request.Status), ct);
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

    /// <summary>
    /// No-code approval-chain builder (05-enhancements-and-roadmap.md Phase 4, "replaces the Phase-1 static
    /// rules"): each WorkflowChainRule.RuleJson names an approver strategy and an optional condition gating
    /// whether that step applies at all, e.g. {"approver":"skip_level_manager","if":{"field":"Days","op":
    /// "&gt;","value":5}} for "manager then skip-level for &gt;5 days" (§J example), or
    /// {"approver":"department_head"} for "department head for designation changes" with no condition.
    /// Supported approver strategies: direct_manager, skip_level_manager (requester's manager's manager),
    /// department_head, specific_employee (fixed {"employeeId":"..."} — e.g. always route Finance approval
    /// to a named person). A rule whose condition doesn't match the submitted payload is skipped entirely
    /// (not added as a step) rather than blocking the chain.
    /// </summary>
    private async Task<List<Guid>> ResolveApproverChainAsync(WorkflowRequestType requestType, Guid requesterEmployeeId, string payloadJson, CancellationToken ct)
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
            var (approverType, condition, specificEmployeeId) = ParseRule(rule.RuleJson);
            if (condition is not null && !EvaluateCondition(condition.Value, payloadJson)) continue;

            Guid? approverId = approverType switch
            {
                "direct_manager" => employee.ManagerId,
                "skip_level_manager" => employee.ManagerId is null ? null
                    : (await _db.Employees.FindAsync(new object[] { employee.ManagerId.Value }, ct))?.ManagerId,
                "department_head" => employee.DepartmentId is null ? null
                    : await _db.Departments.Where(d => d.Id == employee.DepartmentId).Select(d => d.HeadEmployeeId).FirstOrDefaultAsync(ct),
                "specific_employee" => specificEmployeeId,
                _ => null
            };
            if (approverId is not null && approverId != Guid.Empty && approverId != requesterEmployeeId) chain.Add(approverId.Value);
        }
        return chain;
    }

    private Task NotifyApproverAsync(Guid approverEmployeeId, WorkflowRequestType requestType, CancellationToken ct)
        => _notificationService.NotifyAsync(approverEmployeeId, "workflow.approval",
            "New approval request", $"A {requestType} request is waiting for your approval.", ct: ct);

    private readonly record struct RuleCondition(string Field, string Op, decimal Value);

    private static (string? Approver, RuleCondition? Condition, Guid? SpecificEmployeeId) ParseRule(string ruleJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(ruleJson);
            var root = doc.RootElement;
            var approver = root.TryGetProperty("approver", out var approverProp) ? approverProp.GetString() : null;

            Guid? specificEmployeeId = null;
            if (approver == "specific_employee" && root.TryGetProperty("employeeId", out var idProp) && Guid.TryParse(idProp.GetString(), out var parsedId))
                specificEmployeeId = parsedId;

            RuleCondition? condition = null;
            if (root.TryGetProperty("if", out var ifProp) &&
                ifProp.TryGetProperty("field", out var fieldProp) &&
                ifProp.TryGetProperty("op", out var opProp) &&
                ifProp.TryGetProperty("value", out var valueProp) &&
                valueProp.TryGetDecimal(out var value))
            {
                condition = new RuleCondition(fieldProp.GetString() ?? string.Empty, opProp.GetString() ?? string.Empty, value);
            }

            return (approver, condition, specificEmployeeId);
        }
        catch (JsonException)
        {
            return (null, null, null);
        }
    }

    /// <summary>Looks up condition.Field case-insensitively in the submitted payload (top-level properties
    /// only — e.g. LeaveRequest's "Days" or Travel's "EstimatedCost") and compares numerically. A missing
    /// or non-numeric field fails the condition (the rule is skipped) rather than throwing, so a
    /// misconfigured or type-mismatched rule never blocks submission.</summary>
    private static bool EvaluateCondition(RuleCondition condition, string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, condition.Field, StringComparison.OrdinalIgnoreCase)) continue;
                if (!property.Value.TryGetDecimal(out var fieldValue)) return false;

                return condition.Op switch
                {
                    ">" => fieldValue > condition.Value,
                    ">=" => fieldValue >= condition.Value,
                    "<" => fieldValue < condition.Value,
                    "<=" => fieldValue <= condition.Value,
                    "==" => fieldValue == condition.Value,
                    "!=" => fieldValue != condition.Value,
                    _ => false
                };
            }
            return false; // field not present in payload
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
