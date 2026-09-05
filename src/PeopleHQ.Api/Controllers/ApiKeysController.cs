using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Integrations;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/api-keys")]
[RequirePermission(Permissions.ApiKeyWrite)]
public class ApiKeysController : ControllerBase
{
    private readonly ISender _sender;
    public ApiKeysController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _sender.Send(new GetApiKeysQuery()));

    [HttpPost]
    public async Task<IActionResult> Create(CreateApiKeyCommand command)
    {
        var result = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        await _sender.Send(new RevokeApiKeyCommand(id));
        return NoContent();
    }
}
