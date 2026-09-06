using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Insights;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

/// <summary>Phase 5 differentiation insights — manager/admin-facing only, gated by attritionrisk.read.
/// Never exposed to the employee themselves; never wired to any automated action.</summary>
[ApiController]
[Route("api/v1/insights")]
[RequirePermission(Permissions.AttritionRiskRead)]
public class InsightsController : ControllerBase
{
    private readonly ISender _sender;
    public InsightsController(ISender sender) => _sender = sender;

    [HttpGet("attrition-risk/{employeeId:guid}")]
    public async Task<IActionResult> GetAttritionRisk(Guid employeeId)
        => Ok(await _sender.Send(new GetAttritionRiskScoreQuery(employeeId)));

    [HttpGet("attrition-risk/team/{managerId:guid}")]
    public async Task<IActionResult> GetTeamAttritionRisk(Guid managerId)
        => Ok(await _sender.Send(new GetTeamAttritionRiskQuery(managerId)));

    [HttpGet("attendance-anomalies/{employeeId:guid}")]
    public async Task<IActionResult> GetAttendanceAnomalies(Guid employeeId, [FromQuery] int? year = null)
        => Ok(await _sender.Send(new GetAttendanceAnomalyInsightsQuery(employeeId, year ?? DateTime.UtcNow.Year)));
}
