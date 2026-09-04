using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Application.Workflow;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/delegations")]
[Authorize]
public class DelegationsController : ControllerBase
{
    private readonly ISender _sender;
    public DelegationsController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetMine() => Ok(await _sender.Send(new GetMyDelegationsQuery()));

    [HttpPost]
    public async Task<IActionResult> Create(CreateDelegationCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetMine), new { id }, new { id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteDelegationCommand(id));
        return NoContent();
    }
}
