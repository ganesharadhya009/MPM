using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.OrgStructure;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/org-chart")]
public class OrgChartController : ControllerBase
{
    private readonly ISender _sender;
    public OrgChartController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.OrgChartRead)]
    public async Task<IActionResult> Get() => Ok(await _sender.Send(new GetOrgChartQuery()));
}
