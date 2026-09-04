using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.HrProcess;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

/// <summary>
/// Self-service HR Process request submission (01-modules-functional-spec.md §H, Phase 2). Approval,
/// rejection, and withdrawal of these requests reuse the existing generic endpoints in
/// ApprovalsController — one inbox across all request types, per spec.
/// </summary>
[ApiController]
[Route("api/v1/hr-process-requests")]
public class HrProcessRequestsController : ControllerBase
{
    private readonly ISender _sender;
    public HrProcessRequestsController(ISender sender) => _sender = sender;

    [HttpPost("department-change")]
    [RequirePermission(Permissions.HrProcessRequestWrite)]
    public async Task<IActionResult> SubmitDepartmentChange(SubmitDepartmentChangeRequestCommand command)
        => Ok(new { id = await _sender.Send(command) });

    [HttpPost("location-change")]
    [RequirePermission(Permissions.HrProcessRequestWrite)]
    public async Task<IActionResult> SubmitLocationChange(SubmitLocationChangeRequestCommand command)
        => Ok(new { id = await _sender.Send(command) });

    [HttpPost("designation-change")]
    [RequirePermission(Permissions.HrProcessRequestWrite)]
    public async Task<IActionResult> SubmitDesignationChange(SubmitDesignationChangeRequestCommand command)
        => Ok(new { id = await _sender.Send(command) });

    [HttpPost("travel-requests")]
    [RequirePermission(Permissions.HrProcessRequestWrite)]
    public async Task<IActionResult> SubmitTravelRequest(SubmitTravelRequestCommand command)
        => Ok(new { id = await _sender.Send(command) });

    [HttpPost("travel-expenses")]
    [RequirePermission(Permissions.HrProcessRequestWrite)]
    public async Task<IActionResult> SubmitTravelExpense(SubmitTravelExpenseCommand command)
        => Ok(new { id = await _sender.Send(command) });

    [HttpPost("exit-requests")]
    [RequirePermission(Permissions.HrProcessRequestWrite)]
    public async Task<IActionResult> SubmitExitRequest(SubmitExitRequestCommand command)
        => Ok(new { id = await _sender.Send(command) });
}
