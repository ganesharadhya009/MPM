using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Integrations;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/webhooks")]
[RequirePermission(Permissions.WebhookWrite)]
public class WebhooksController : ControllerBase
{
    private readonly ISender _sender;
    public WebhooksController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _sender.Send(new GetWebhookSubscriptionsQuery()));

    [HttpPost]
    public async Task<IActionResult> Create(CreateWebhookSubscriptionCommand command)
    {
        var result = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteWebhookSubscriptionCommand(id));
        return NoContent();
    }

    [HttpGet("{id:guid}/deliveries")]
    public async Task<IActionResult> GetDeliveries(Guid id) => Ok(await _sender.Send(new GetWebhookDeliveriesQuery(id)));
}
