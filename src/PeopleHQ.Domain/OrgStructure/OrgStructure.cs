using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.OrgStructure;

public class Location : TenantOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public Guid? HolidayCalendarId { get; set; }
}

/// <summary>Tenant-owned (a tenant can ship starter templates, but instances are tenant data).</summary>
public class HolidayCalendar : TenantOwnedEntity
{
    public string Name { get; set; } = string.Empty;
}

public class Holiday : TenantOwnedEntity
{
    public Guid HolidayCalendarId { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsOptional { get; set; }
}

public class Department : TenantOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentDepartmentId { get; set; }
    public Guid? HeadEmployeeId { get; set; }
}

public class Designation : TenantOwnedEntity
{
    public string Title { get; set; } = string.Empty;
    public int? Grade { get; set; }
}

/// <summary>
/// "Beyond Zoho" — effective-dated org-structure history (02-data-model-erd.md
/// "Key modeling decisions" #2). Answers "who was X's manager on date Y".
/// </summary>
public class EmployeePositionHistory : TenantOwnedEntity
{
    public Guid EmployeeId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? DesignationId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? ManagerId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}
