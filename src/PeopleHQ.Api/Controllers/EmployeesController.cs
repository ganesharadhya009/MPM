using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Employees;
using PeopleHQ.Domain.Employees;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/employees")]
public class EmployeesController : ControllerBase
{
    private readonly ISender _sender;
    public EmployeesController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.EmployeeRead)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] Guid? departmentId = null, [FromQuery] EmployeeStatus? status = null)
        => Ok(await _sender.Send(new GetEmployeesQuery(page, pageSize, departmentId, status)));

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.EmployeeRead)]
    public async Task<IActionResult> GetById(Guid id) => Ok(await _sender.Send(new GetEmployeeByIdQuery(id)));

    [HttpGet("{id:guid}/reportees")]
    [RequirePermission(Permissions.EmployeeRead)]
    public async Task<IActionResult> GetReportees(Guid id, [FromQuery] bool includeIndirect = false)
        => Ok(await _sender.Send(new GetReporteesQuery(id, includeIndirect)));

    [HttpPost]
    [RequirePermission(Permissions.EmployeeWrite)]
    public async Task<IActionResult> Create(CreateEmployeeCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.EmployeeWrite)]
    public async Task<IActionResult> Update(Guid id, UpdateEmployeeCommand command)
    {
        if (id != command.Id) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpPut("{id:guid}/manager")]
    [RequirePermission(Permissions.EmployeeWrite)]
    public async Task<IActionResult> ChangeManager(Guid id, ChangeManagerRequestBody body)
    {
        await _sender.Send(new ChangeEmployeeManagerCommand(id, body.NewManagerId));
        return NoContent();
    }

    [HttpPost("{id:guid}/exit")]
    [RequirePermission(Permissions.EmployeeWrite)]
    public async Task<IActionResult> Exit(Guid id, ExitEmployeeRequestBody body)
    {
        await _sender.Send(new ExitEmployeeCommand(id, body.ExitDate));
        return NoContent();
    }
}

public record ChangeManagerRequestBody(Guid? NewManagerId);
public record ExitEmployeeRequestBody(DateOnly ExitDate);
