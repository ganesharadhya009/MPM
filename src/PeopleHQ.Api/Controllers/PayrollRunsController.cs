using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Payroll;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/payroll-runs")]
public class PayrollRunsController : ControllerBase
{
    private readonly ISender _sender;
    public PayrollRunsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.PayrollRunWrite)]
    public async Task<IActionResult> GetAll([FromQuery] int? periodYear = null)
        => Ok(await _sender.Send(new GetPayrollRunsQuery(periodYear)));

    [HttpGet("{id:guid}/items")]
    [RequirePermission(Permissions.PayrollRunWrite)]
    public async Task<IActionResult> GetItems(Guid id)
        => Ok(await _sender.Send(new GetPayrollRunItemsQuery(id)));

    [HttpPost]
    [RequirePermission(Permissions.PayrollRunWrite)]
    public async Task<IActionResult> Create(CreatePayrollRunCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpPost("{id:guid}/compute")]
    [RequirePermission(Permissions.PayrollRunWrite)]
    public async Task<IActionResult> Compute(Guid id)
    {
        await _sender.Send(new ComputePayrollRunCommand(id));
        return NoContent();
    }

    [HttpPost("{id:guid}/submit")]
    [RequirePermission(Permissions.PayrollRunWrite)]
    public async Task<IActionResult> Submit(Guid id)
    {
        await _sender.Send(new SubmitPayrollRunForApprovalCommand(id));
        return NoContent();
    }

    [HttpPost("{id:guid}/lock")]
    [RequirePermission(Permissions.PayrollRunApprove)]
    public async Task<IActionResult> Lock(Guid id)
    {
        await _sender.Send(new LockPayrollRunCommand(id));
        return NoContent();
    }

    [HttpPost("{id:guid}/mark-paid")]
    [RequirePermission(Permissions.PayrollRunApprove)]
    public async Task<IActionResult> MarkPaid(Guid id)
    {
        await _sender.Send(new MarkPayrollRunPaidCommand(id));
        return NoContent();
    }

    [HttpPost("items/{itemId:guid}/override-line")]
    [RequirePermission(Permissions.PayrollRunWrite)]
    public async Task<IActionResult> OverrideLine(Guid itemId, OverridePayrollRunItemLineCommand command)
    {
        if (itemId != command.PayrollRunItemId) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpPost("{id:guid}/generate-payslips")]
    [RequirePermission(Permissions.PayrollRunWrite)]
    public async Task<IActionResult> GeneratePayslips(Guid id)
    {
        await _sender.Send(new GeneratePayslipsCommand(id));
        return NoContent();
    }
}
