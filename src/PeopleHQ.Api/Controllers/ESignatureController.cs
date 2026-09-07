using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.ESignature;
using PeopleHQ.Domain.ESignature;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/esignature-requests")]
public class ESignatureController : ControllerBase
{
    private readonly ISender _sender;
    public ESignatureController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.ESignatureSign)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? employeeId = null, [FromQuery] ESignatureStatus? status = null)
        => Ok(await _sender.Send(new GetESignatureRequestsQuery(employeeId, status)));

    [HttpPost]
    [RequirePermission(Permissions.ESignatureRequestWrite)]
    public async Task<IActionResult> Create(CreateESignatureRequestCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpPost("{id:guid}/sign")]
    [RequirePermission(Permissions.ESignatureSign)]
    public async Task<IActionResult> Sign(Guid id, SignDocumentCommand command)
    {
        if (id != command.Id) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpPost("{id:guid}/decline")]
    [RequirePermission(Permissions.ESignatureSign)]
    public async Task<IActionResult> Decline(Guid id)
    {
        await _sender.Send(new DeclineDocumentCommand(id));
        return NoContent();
    }
}
