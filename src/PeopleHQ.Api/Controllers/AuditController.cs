using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Auditing;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/audit-log")]
[RequirePermission(Permissions.AuditLogRead)]
public class AuditController : ControllerBase
{
    private readonly ISender _sender;
    public AuditController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? entityName, [FromQuery] Guid? entityId, [FromQuery] Guid? actorUserId,
        [FromQuery] DateTime? startDateUtc, [FromQuery] DateTime? endDateUtc,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(await _sender.Send(new GetAuditLogQuery(entityName, entityId, actorUserId, startDateUtc, endDateUtc, page, pageSize)));
}

/// <summary>Data export/deletion center ("most needed options" #7).</summary>
[ApiController]
[Route("api/v1/data-compliance")]
public class DataComplianceController : ControllerBase
{
    private readonly ISender _sender;
    public DataComplianceController(ISender sender) => _sender = sender;

    [HttpGet("employees/{employeeId:guid}/export")]
    [RequirePermission(Permissions.DataExportRead)]
    public async Task<IActionResult> ExportEmployeeData(Guid employeeId)
        => Ok(await _sender.Send(new ExportEmployeeDataCommand(employeeId)));

    [HttpPost("employees/{employeeId:guid}/anonymize")]
    [RequirePermission(Permissions.DataErasureWrite)]
    public async Task<IActionResult> AnonymizeEmployeeData(Guid employeeId)
    {
        await _sender.Send(new AnonymizeEmployeeDataCommand(employeeId));
        return NoContent();
    }
}
