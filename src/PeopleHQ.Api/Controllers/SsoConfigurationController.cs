using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Auth;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/sso-configuration")]
[RequirePermission(Permissions.SsoConfigWrite)]
public class SsoConfigurationController : ControllerBase
{
    private readonly ISender _sender;
    public SsoConfigurationController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _sender.Send(new GetSsoConfigurationQuery()));

    [HttpPut]
    public async Task<IActionResult> Upsert(UpsertSsoConfigurationCommand command)
    {
        await _sender.Send(command);
        return NoContent();
    }
}
