using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Billing;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/billing")]
public class BillingController : ControllerBase
{
    private readonly ISender _sender;
    public BillingController(ISender sender) => _sender = sender;

    [HttpGet("seat-usage")]
    [RequirePermission(Permissions.BillingRead)]
    public async Task<IActionResult> GetSeatUsage() => Ok(await _sender.Send(new GetSeatUsageQuery()));

    [HttpGet("plans")]
    [RequirePermission(Permissions.BillingRead)]
    public async Task<IActionResult> GetPlans() => Ok(await _sender.Send(new GetPlansQuery()));

    [HttpPost("upgrade")]
    [RequirePermission(Permissions.BillingWrite)]
    public async Task<IActionResult> Upgrade(UpgradePlanCommand command)
    {
        var result = await _sender.Send(command);
        if (!result.Succeeded) return Problem(title: "Plan upgrade failed", statusCode: 400, detail: result.Error);
        return Ok(new { invoiceId = result.InvoiceId });
    }

    [HttpGet("invoices")]
    [RequirePermission(Permissions.BillingRead)]
    public async Task<IActionResult> GetInvoices() => Ok(await _sender.Send(new GetInvoicesQuery()));
}
