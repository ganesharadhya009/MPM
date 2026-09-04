using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Payroll;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/statutory-settings")]
public class StatutorySettingsController : ControllerBase
{
    private readonly ISender _sender;
    public StatutorySettingsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.StatutorySettingsWrite)]
    public async Task<IActionResult> Get() => Ok(await _sender.Send(new GetStatutorySettingsQuery()));

    [HttpPut]
    [RequirePermission(Permissions.StatutorySettingsWrite)]
    public async Task<IActionResult> Upsert(UpsertStatutorySettingsCommand command)
    {
        await _sender.Send(command);
        return NoContent();
    }

    [HttpGet("pt-slabs")]
    [RequirePermission(Permissions.StatutorySettingsWrite)]
    public async Task<IActionResult> GetPtSlabs([FromQuery] string? state = null)
        => Ok(await _sender.Send(new GetPtSlabsQuery(state)));

    [HttpPost("pt-slabs")]
    [RequirePermission(Permissions.StatutorySettingsWrite)]
    public async Task<IActionResult> CreatePtSlab(CreatePtSlabCommand command)
    {
        var id = await _sender.Send(command);
        return Ok(new { id });
    }

    [HttpDelete("pt-slabs/{id:guid}")]
    [RequirePermission(Permissions.StatutorySettingsWrite)]
    public async Task<IActionResult> DeletePtSlab(Guid id)
    {
        await _sender.Send(new DeletePtSlabCommand(id));
        return NoContent();
    }
}
