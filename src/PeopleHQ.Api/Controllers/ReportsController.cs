using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Reports;
using PeopleHQ.Domain.Identity;
using PeopleHQ.Infrastructure.Common;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
[RequirePermission(Permissions.ReportRead)]
public class ReportsController : ControllerBase
{
    private readonly ISender _sender;
    public ReportsController(ISender sender) => _sender = sender;

    [HttpGet("headcount")]
    public async Task<IActionResult> GetHeadcount() => Ok(await _sender.Send(new GetHeadcountReportQuery()));

    [HttpGet("attrition")]
    public async Task<IActionResult> GetAttrition([FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate)
        => Ok(await _sender.Send(new GetAttritionReportQuery(startDate, endDate)));

    [HttpGet("leave-utilization")]
    public async Task<IActionResult> GetLeaveUtilization([FromQuery] int year, [FromQuery] string format = "json")
    {
        var rows = await _sender.Send(new GetLeaveUtilizationReportQuery(year));
        return format.Equals("csv", StringComparison.OrdinalIgnoreCase)
            ? File(CsvExporter.ToCsvBytes(rows), "text/csv", "leave-utilization.csv")
            : Ok(rows);
    }

    [HttpGet("attendance-summary")]
    public async Task<IActionResult> GetAttendanceSummary([FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate, [FromQuery] string format = "json")
    {
        var rows = await _sender.Send(new GetAttendanceSummaryReportQuery(startDate, endDate));
        return format.Equals("csv", StringComparison.OrdinalIgnoreCase)
            ? File(CsvExporter.ToCsvBytes(rows), "text/csv", "attendance-summary.csv")
            : Ok(rows);
    }

    [HttpGet("onboarding-time-to-productivity")]
    public async Task<IActionResult> GetOnboardingTimeToProductivity([FromQuery] string format = "json")
    {
        var rows = await _sender.Send(new GetOnboardingTimeToProductivityReportQuery());
        return format.Equals("csv", StringComparison.OrdinalIgnoreCase)
            ? File(CsvExporter.ToCsvBytes(rows), "text/csv", "onboarding-time-to-productivity.csv")
            : Ok(rows);
    }

    [HttpGet("approval-sla")]
    public async Task<IActionResult> GetApprovalSla([FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate, [FromQuery] string format = "json")
    {
        var rows = await _sender.Send(new GetApprovalSlaReportQuery(startDate, endDate));
        return format.Equals("csv", StringComparison.OrdinalIgnoreCase)
            ? File(CsvExporter.ToCsvBytes(rows), "text/csv", "approval-sla.csv")
            : Ok(rows);
    }
}
