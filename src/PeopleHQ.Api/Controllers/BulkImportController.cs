using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.BulkImport;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

/// <summary>FR-ORG-06: CSV bulk import for org structure master data and Employees, with a mandatory
/// validation-preview step before commit.</summary>
[ApiController]
[Route("api/v1/bulk-import")]
[RequirePermission(Permissions.BulkImportWrite)]
public class BulkImportController : ControllerBase
{
    private readonly ISender _sender;
    public BulkImportController(ISender sender) => _sender = sender;

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(PreviewBulkImportCommand command)
        => Ok(await _sender.Send(command));

    [HttpPost("commit")]
    public async Task<IActionResult> Commit(CommitBulkImportCommand command)
        => Ok(await _sender.Send(command));
}
