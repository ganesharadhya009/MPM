using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.Engagement;

public enum AssetStatus { InStock, Assigned, Returned, Retired }
public enum HelpdeskTicketStatus { Open, InProgress, Resolved, Closed }

/// <summary>Single-question pulse/eNPS surveys (01-modules-functional-spec.md §L "Beyond Zoho").</summary>
public class Survey : TenantOwnedEntity
{
    public string Question { get; set; } = string.Empty;
    public bool IsAnonymous { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class SurveyResponse : TenantOwnedEntity
{
    public Guid SurveyId { get; set; }
    public Guid? RespondentEmployeeId { get; set; } // null when anonymous
    public int Score { get; set; }
    public string? Comment { get; set; }
}

/// <summary>"Most needed options" #2 — laptops/equipment issued to an employee, returned on exit.</summary>
public class Asset : TenantOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string SerialNo { get; set; } = string.Empty;
    public Guid? AssignedEmployeeId { get; set; }
    public AssetStatus Status { get; set; } = AssetStatus.InStock;
}

/// <summary>"Most needed options" #3 — HR Helpdesk/case management with SLA tracking.</summary>
public class HelpdeskTicket : TenantOwnedEntity
{
    public Guid RaisedByEmployeeId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Subject { get; set; } = string.Empty;
    public HelpdeskTicketStatus Status { get; set; } = HelpdeskTicketStatus.Open;
    public Guid? AssignedToEmployeeId { get; set; }
    public DateTime? SlaDueAtUtc { get; set; }
}

public class Announcement : TenantOwnedEntity
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    /// <summary>Audience filter: all/department/location — serialized JSON.</summary>
    public string AudienceJson { get; set; } = "{\"scope\":\"all\"}";
    public DateTime? PublishedAtUtc { get; set; }
}
