using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Onboarding;
using PeopleHQ.Domain.Identity;
using PeopleHQ.Domain.Onboarding;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/onboarding-tasks")]
public class OnboardingTasksController : ControllerBase
{
    private readonly ISender _sender;
    public OnboardingTasksController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.OnboardingTaskRead)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? candidateId, [FromQuery] Guid? employeeId, [FromQuery] OnboardingTaskStatus? status)
        => Ok(await _sender.Send(new GetOnboardingTasksQuery(candidateId, employeeId, status)));

    [HttpPost]
    [RequirePermission(Permissions.OnboardingTaskWrite)]
    public async Task<IActionResult> Create(CreateOnboardingTaskCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpPost("{id:guid}/complete")]
    [RequirePermission(Permissions.OnboardingTaskWrite)]
    public async Task<IActionResult> Complete(Guid id)
    {
        await _sender.Send(new CompleteOnboardingTaskCommand(id));
        return NoContent();
    }
}
