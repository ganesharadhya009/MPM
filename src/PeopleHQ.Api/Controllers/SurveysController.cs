using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Engagement;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/surveys")]
public class SurveysController : ControllerBase
{
    private readonly ISender _sender;
    public SurveysController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.SurveyRespond)]
    public async Task<IActionResult> GetActive() => Ok(await _sender.Send(new GetActiveSurveysQuery()));

    [HttpPost]
    [RequirePermission(Permissions.SurveyWrite)]
    public async Task<IActionResult> Create(CreateSurveyCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetActive), new { id }, new { id });
    }

    [HttpPost("{id:guid}/deactivate")]
    [RequirePermission(Permissions.SurveyWrite)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await _sender.Send(new DeactivateSurveyCommand(id));
        return NoContent();
    }

    [HttpPost("{id:guid}/responses")]
    [RequirePermission(Permissions.SurveyRespond)]
    public async Task<IActionResult> Respond(Guid id, SubmitSurveyResponseCommand command)
    {
        if (id != command.SurveyId) return Problem(title: "Id mismatch", statusCode: 400);
        var responseId = await _sender.Send(command);
        return Ok(new { id = responseId });
    }

    [HttpGet("{id:guid}/results")]
    [RequirePermission(Permissions.SurveyWrite)]
    public async Task<IActionResult> GetResults(Guid id) => Ok(await _sender.Send(new GetSurveyResultsQuery(id)));
}
