using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Workflow;
using PeopleHQ.Domain.Identity;
using PeopleHQ.Domain.Workflow;

namespace PeopleHQ.Api.Controllers;

/// <summary>The unified approvals inbox (Phase 2 ESS/MSS §, exposed here since the generic engine is Phase 1) —
/// every request type (Leave, Regularization, Timesheet, Payroll Run, ...) surfaces through this one endpoint set.</summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class ApprovalsController : ControllerBase
{
    private readonly ISender _sender;
    public ApprovalsController(ISender sender) => _sender = sender;

    [HttpGet("approvals/pending")]
    [RequirePermission(Permissions.WorkflowApprove)]
    public async Task<IActionResult> GetPending() => Ok(await _sender.Send(new GetMyPendingApprovalsQuery()));

    [HttpPost("approvals/{workflowRequestId:guid}/approve")]
    [RequirePermission(Permissions.WorkflowApprove)]
    public async Task<IActionResult> Approve(Guid workflowRequestId, ApprovalActionRequestBody body)
    {
        await _sender.Send(new ApproveWorkflowRequestCommand(workflowRequestId, body.Comment));
        return NoContent();
    }

    [HttpPost("approvals/{workflowRequestId:guid}/reject")]
    [RequirePermission(Permissions.WorkflowApprove)]
    public async Task<IActionResult> Reject(Guid workflowRequestId, ApprovalActionRequestBody body)
    {
        await _sender.Send(new RejectWorkflowRequestCommand(workflowRequestId, body.Comment));
        return NoContent();
    }

    [HttpGet("workflow-requests/mine")]
    public async Task<IActionResult> GetMine([FromQuery] WorkflowStatus? status)
        => Ok(await _sender.Send(new GetMyRequestsQuery(status)));

    [HttpPost("workflow-requests/{workflowRequestId:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid workflowRequestId)
    {
        await _sender.Send(new WithdrawWorkflowRequestCommand(workflowRequestId));
        return NoContent();
    }
}

public record ApprovalActionRequestBody(string? Comment);
