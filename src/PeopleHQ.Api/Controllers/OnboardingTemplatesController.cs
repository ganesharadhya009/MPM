using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Onboarding;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/onboarding-templates")]
public class OnboardingTemplatesController : ControllerBase
{
    private readonly ISender _sender;
    public OnboardingTemplatesController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.OnboardingTemplateRead)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        => Ok(await _sender.Send(new GetOnboardingChecklistTemplatesQuery(page, pageSize)));

    [HttpPost]
    [RequirePermission(Permissions.OnboardingTemplateWrite)]
    public async Task<IActionResult> Create(CreateOnboardingChecklistTemplateCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.OnboardingTemplateWrite)]
    public async Task<IActionResult> Update(Guid id, UpdateOnboardingChecklistTemplateCommand command)
    {
        if (id != command.Id) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.OnboardingTemplateWrite)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteOnboardingChecklistTemplateCommand(id));
        return NoContent();
    }
}
