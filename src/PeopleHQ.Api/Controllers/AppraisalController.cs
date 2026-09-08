using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Appraisal;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/appraisal-templates")]
[RequirePermission(Permissions.AppraisalTemplateWrite)]
public class AppraisalTemplatesController : ControllerBase
{
    private readonly ISender _sender;
    public AppraisalTemplatesController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _sender.Send(new GetAppraisalTemplatesQuery()));

    [HttpPost]
    public async Task<IActionResult> Create(CreateAppraisalTemplateCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }
}

[ApiController]
[Route("api/v1/appraisal-cycles")]
public class AppraisalCyclesController : ControllerBase
{
    private readonly ISender _sender;
    public AppraisalCyclesController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.AppraisalReviewWrite)]
    public async Task<IActionResult> GetAll() => Ok(await _sender.Send(new GetAppraisalCyclesQuery()));

    [HttpPost]
    [RequirePermission(Permissions.AppraisalCycleWrite)]
    public async Task<IActionResult> Create(CreateAppraisalCycleCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpPost("{id:guid}/activate")]
    [RequirePermission(Permissions.AppraisalCycleWrite)]
    public async Task<IActionResult> Activate(Guid id)
    {
        await _sender.Send(new ActivateAppraisalCycleCommand(id));
        return NoContent();
    }

    [HttpPost("{id:guid}/close")]
    [RequirePermission(Permissions.AppraisalCycleWrite)]
    public async Task<IActionResult> Close(Guid id)
    {
        await _sender.Send(new CloseAppraisalCycleCommand(id));
        return NoContent();
    }

    [HttpGet("{id:guid}/team-reviews")]
    [RequirePermission(Permissions.AppraisalReviewManage)]
    public async Task<IActionResult> GetTeamReviews(Guid id) => Ok(await _sender.Send(new GetTeamAppraisalReviewsQuery(id)));
}

[ApiController]
[Route("api/v1/appraisal-reviews")]
[RequirePermission(Permissions.AppraisalReviewWrite)]
public class AppraisalReviewsController : ControllerBase
{
    private readonly ISender _sender;
    public AppraisalReviewsController(ISender sender) => _sender = sender;

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine() => Ok(await _sender.Send(new GetMyAppraisalReviewsQuery()));

    [HttpPost("{id:guid}/self-review")]
    public async Task<IActionResult> SubmitSelfReview(Guid id, SubmitSelfReviewCommand command)
    {
        if (id != command.ReviewId) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpPost("{id:guid}/manager-review")]
    [RequirePermission(Permissions.AppraisalReviewManage)]
    public async Task<IActionResult> SubmitManagerReview(Guid id, SubmitManagerReviewCommand command)
    {
        if (id != command.ReviewId) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpPost("{id:guid}/finalize")]
    [RequirePermission(Permissions.AppraisalReviewManage)]
    public async Task<IActionResult> Finalize(Guid id, FinalizeAppraisalReviewCommand command)
    {
        if (id != command.ReviewId) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }
}
