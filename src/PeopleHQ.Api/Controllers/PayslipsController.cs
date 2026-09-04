using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Payroll;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/payslips")]
public class PayslipsController : ControllerBase
{
    private readonly ISender _sender;
    public PayslipsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.PayslipReadOwn)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? employeeId = null)
        => Ok(await _sender.Send(new GetPayslipsQuery(employeeId)));
}
