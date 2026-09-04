using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.Employees;

public enum EmploymentType { FullTime, PartTime, Contractor }
public enum EmployeeStatus { Invited, Active, OnLeave, Suspended, Exited }
public enum DocumentType { IdProof, Contract, Certification, Other }
public enum CustomFieldType { Text, Number, Date, Dropdown, Checkbox }

public class Employee : TenantOwnedEntity
{
    public Guid? UserId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? PersonalEmail { get; set; }
    public string? WorkEmail { get; set; }
    public string? Phone { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? DesignationId { get; set; }
    public Guid? ManagerId { get; set; }
    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;
    public DateOnly JoinDate { get; set; }
    public DateOnly? ExitDate { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Invited;
}

public class EmployeeDocument : TenantOwnedEntity
{
    public Guid EmployeeId { get; set; }
    public DocumentType DocType { get; set; }
    public string BlobUrl { get; set; } = string.Empty;
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    public DateOnly? ExpiresAt { get; set; }
}

/// <summary>"Beyond Zoho" addition (01-modules-functional-spec.md §D).</summary>
public class EmployeeSkill : TenantOwnedEntity
{
    public Guid EmployeeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Level { get; set; }
    public DateOnly? ExpiresAt { get; set; }
}

public class CustomFieldDefinition : TenantOwnedEntity
{
    public string Entity { get; set; } = "Employee";
    public string Label { get; set; } = string.Empty;
    public CustomFieldType FieldType { get; set; }
    public string? OptionsJson { get; set; } // for Dropdown
    public bool IsRequired { get; set; }
}

public class EmployeeCustomFieldValue
{
    public Guid EmployeeId { get; set; }
    public Guid FieldDefinitionId { get; set; }
    public string? Value { get; set; } // cast per FieldType in the application layer
}
