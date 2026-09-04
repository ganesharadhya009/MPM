using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Employees;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/custom-field-definitions")]
public class CustomFieldDefinitionsController : ControllerBase
{
    private readonly ISender _sender;
    public CustomFieldDefinitionsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.EmployeeRead)]
    public async Task<IActionResult> GetAll([FromQuery] string entity = "Employee")
        => Ok(await _sender.Send(new GetCustomFieldDefinitionsQuery(entity)));

    [HttpPost]
    [RequirePermission(Permissions.CustomFieldDefinitionWrite)]
    public async Task<IActionResult> Create(CreateCustomFieldDefinitionCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.CustomFieldDefinitionWrite)]
    public async Task<IActionResult> Update(Guid id, UpdateCustomFieldDefinitionCommand command)
    {
        if (id != command.Id) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.CustomFieldDefinitionWrite)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteCustomFieldDefinitionCommand(id));
        return NoContent();
    }

    [HttpGet("/api/v1/employees/{employeeId:guid}/custom-field-values")]
    [RequirePermission(Permissions.EmployeeRead)]
    public async Task<IActionResult> GetEmployeeValues(Guid employeeId)
        => Ok(await _sender.Send(new GetEmployeeCustomFieldValuesQuery(employeeId)));

    [HttpPut("/api/v1/employees/{employeeId:guid}/custom-field-values")]
    [RequirePermission(Permissions.CustomFieldValueWrite)]
    public async Task<IActionResult> SetEmployeeValues(Guid employeeId, SetEmployeeCustomFieldValuesCommand command)
    {
        if (employeeId != command.EmployeeId) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }
}
