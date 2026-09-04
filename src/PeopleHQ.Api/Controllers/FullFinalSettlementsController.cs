using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Payroll;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/full-final-settlements")]
public class FullFinalSettlementsController : ControllerBase
{
    private readonly ISender _sender;
    public FullFinalSettlementsController(ISender sender) => _sender = sender;

    [HttpPost]
    [RequirePermission(Permissions.PayrollRunWrite)]
    public async Task<IActionResult> Compute(ComputeFullFinalSettlementCommand command)
    {
        var id = await _sender.Send(command);
        return Ok(new { id });
    }

    [HttpGet("{employeeId:guid}")]
    [RequirePermission(Permissions.PayrollRunWrite)]
    public async Task<IActionResult> Get(Guid employeeId)
        => Ok(await _sender.Send(new GetFullFinalSettlementQuery(employeeId)));
}
