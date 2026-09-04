using MediatR;

namespace PeopleHQ.Application.BulkImport;

/// <summary>
/// FR-ORG-06: CSV import for Locations/Departments/Designations/Employees with a validation-preview step —
/// every row's pass/fail is reported before any row commits. Commit is all-or-nothing by default
/// (PartialCommit=false): if any row is invalid, nothing is written. PartialCommit=true (a tenant setting
/// per the FR) commits the valid rows and returns the invalid ones as a downloadable error report.
///
/// Expected CSV columns per entity type (header row required, case-insensitive):
///  - Location: Name (required), Address, TimeZone
///  - Department: Name (required), ParentDepartmentName, HeadEmployeeCode
///  - Designation: Title (required), Grade
///  - Employee: FirstName, LastName, JoinDate [yyyy-MM-dd] (required), WorkEmail, PersonalEmail, Phone,
///    DepartmentName, LocationName, DesignationName, ManagerEmployeeCode, EmploymentType [FullTime/PartTime/Contractor]
///
/// v1 simplification: cross-row references (e.g. a Department's ParentDepartmentName) resolve only against
/// rows already committed to the database, not other rows in the same file — multi-level hierarchies import
/// top-down across separate file uploads. Documented, not yet a tracked follow-up ticket.
/// </summary>
public enum BulkImportEntityType { Location, Department, Designation, Employee }

public record BulkImportRowResult(int RowNumber, bool IsValid, string? ErrorMessage);
public record BulkImportPreviewResult(int TotalRows, int ValidRows, int InvalidRows, IReadOnlyList<BulkImportRowResult> Rows);
public record BulkImportCommitResult(int TotalRows, int CommittedRows, int SkippedRows, IReadOnlyList<BulkImportRowResult> Rows);

public record PreviewBulkImportCommand(BulkImportEntityType EntityType, string CsvContent) : IRequest<BulkImportPreviewResult>;
public record CommitBulkImportCommand(BulkImportEntityType EntityType, string CsvContent, bool PartialCommit) : IRequest<BulkImportCommitResult>;
