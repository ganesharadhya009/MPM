using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Payroll;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/salary-structures")]
public class SalaryStructuresController : ControllerBase
{
    private readonly ISender _sender;
    public SalaryStructuresController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.SalaryStructureWrite)]
    public async Task<IActionResult> GetAll() => Ok(await _sender.Send(new GetSalaryStructuresQuery()));

    [HttpPost]
    [RequirePermission(Permissions.SalaryStructureWrite)]
    public async Task<IActionResult> Create(CreateSalaryStructureCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.SalaryStructureWrite)]
    public async Task<IActionResult> Update(Guid id, UpdateSalaryStructureCommand command)
    {
        if (id != command.Id) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.SalaryStructureWrite)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteSalaryStructureCommand(id));
        return NoContent();
    }

    [HttpPost("assign")]
    [RequirePermission(Permissions.SalaryAssignmentWrite)]
    public async Task<IActionResult> AssignSalary(AssignSalaryCommand command)
    {
        var id = await _sender.Send(command);
        return Ok(new { id });
    }

    [HttpGet("employees/{employeeId:guid}/history")]
    [RequirePermission(Permissions.SalaryAssignmentRead)]
    public async Task<IActionResult> GetSalaryHistory(Guid employeeId)
        => Ok(await _sender.Send(new GetEmployeeSalaryHistoryQuery(employeeId)));
}
