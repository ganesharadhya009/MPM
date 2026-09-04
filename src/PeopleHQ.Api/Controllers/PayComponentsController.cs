using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Payroll;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/pay-components")]
public class PayComponentsController : ControllerBase
{
    private readonly ISender _sender;
    public PayComponentsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.PayComponentWrite)]
    public async Task<IActionResult> GetAll() => Ok(await _sender.Send(new GetPayComponentsQuery()));

    [HttpPost]
    [RequirePermission(Permissions.PayComponentWrite)]
    public async Task<IActionResult> Create(CreatePayComponentCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.PayComponentWrite)]
    public async Task<IActionResult> Update(Guid id, UpdatePayComponentCommand command)
    {
        if (id != command.Id) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.PayComponentWrite)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeletePayComponentCommand(id));
        return NoContent();
    }
}
