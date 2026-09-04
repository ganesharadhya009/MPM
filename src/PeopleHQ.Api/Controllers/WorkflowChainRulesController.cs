using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Workflow;
using PeopleHQ.Domain.Identity;
using PeopleHQ.Domain.Workflow;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/workflow-chain-rules")]
public class WorkflowChainRulesController : ControllerBase
{
    private readonly ISender _sender;
    public WorkflowChainRulesController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.WorkflowChainRuleWrite)]
    public async Task<IActionResult> GetByType([FromQuery] WorkflowRequestType requestType)
        => Ok(await _sender.Send(new GetWorkflowChainRulesQuery(requestType)));

    [HttpPost]
    [RequirePermission(Permissions.WorkflowChainRuleWrite)]
    public async Task<IActionResult> Create(CreateWorkflowChainRuleCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetByType), new { requestType = command.RequestType }, new { id });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.WorkflowChainRuleWrite)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteWorkflowChainRuleCommand(id));
        return NoContent();
    }
}
