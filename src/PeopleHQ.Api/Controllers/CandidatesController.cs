using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Onboarding;
using PeopleHQ.Domain.Identity;
using PeopleHQ.Domain.Onboarding;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/candidates")]
public class CandidatesController : ControllerBase
{
    private readonly ISender _sender;
    public CandidatesController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.CandidateRead)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] CandidateStage? stage = null)
        => Ok(await _sender.Send(new GetCandidatesQuery(page, pageSize, stage)));

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.CandidateRead)]
    public async Task<IActionResult> GetById(Guid id) => Ok(await _sender.Send(new GetCandidateByIdQuery(id)));

    [HttpPost]
    [RequirePermission(Permissions.CandidateWrite)]
    public async Task<IActionResult> Create(CreateCandidateCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.CandidateWrite)]
    public async Task<IActionResult> Update(Guid id, UpdateCandidateCommand command)
    {
        if (id != command.Id) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpPut("{id:guid}/stage")]
    [RequirePermission(Permissions.CandidateWrite)]
    public async Task<IActionResult> UpdateStage(Guid id, UpdateCandidateStageRequestBody body)
    {
        await _sender.Send(new UpdateCandidateStageCommand(id, body.Stage));
        return NoContent();
    }

    [HttpPost("{id:guid}/convert")]
    [RequirePermission(Permissions.CandidateWrite)]
    public async Task<IActionResult> Convert(Guid id, ConvertCandidateRequestBody body)
    {
        var employeeId = await _sender.Send(new ConvertCandidateToEmployeeCommand(
            id, body.WorkEmail, body.DepartmentId, body.LocationId, body.ManagerId, body.JoinDate));
        return Ok(new { employeeId });
    }
}

public record UpdateCandidateStageRequestBody(CandidateStage Stage);
public record ConvertCandidateRequestBody(string? WorkEmail, Guid? DepartmentId, Guid? LocationId, Guid? ManagerId, DateOnly JoinDate);
