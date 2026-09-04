using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Payroll;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/investment-declarations")]
public class InvestmentDeclarationsController : ControllerBase
{
    private readonly ISender _sender;
    public InvestmentDeclarationsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.InvestmentDeclarationWrite)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? employeeId = null, [FromQuery] string? financialYear = null)
        => Ok(await _sender.Send(new GetInvestmentDeclarationsQuery(employeeId, financialYear)));

    [HttpPost]
    [RequirePermission(Permissions.InvestmentDeclarationWrite)]
    public async Task<IActionResult> Create(CreateInvestmentDeclarationCommand command)
    {
        var id = await _sender.Send(command);
        return Ok(new { id });
    }

    [HttpPost("{id:guid}/verify")]
    [RequirePermission(Permissions.InvestmentDeclarationVerify)]
    public async Task<IActionResult> Verify(Guid id, VerifyInvestmentDeclarationCommand command)
    {
        if (id != command.Id) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpPost("tax-regime")]
    [RequirePermission(Permissions.InvestmentDeclarationWrite)]
    public async Task<IActionResult> SelectTaxRegime(SelectTaxRegimeCommand command)
    {
        await _sender.Send(command);
        return NoContent();
    }

    [HttpGet("tax-regime")]
    [RequirePermission(Permissions.InvestmentDeclarationWrite)]
    public async Task<IActionResult> GetTaxRegime([FromQuery] Guid employeeId, [FromQuery] string financialYear)
        => Ok(await _sender.Send(new GetTaxRegimeSelectionQuery(employeeId, financialYear)));
}
