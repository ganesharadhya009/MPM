using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.BulkImport;
using PeopleHQ.Application.Employees;
using PeopleHQ.Application.OrgStructure;
using PeopleHQ.Domain.Employees;
using PeopleHQ.Infrastructure.Common;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.BulkImport;

/// <summary>Result of validating one CSV row against the target entity type; Command is populated only
/// when IsValid, ready to dispatch via ISender on commit.</summary>
internal record RowValidationResult(bool IsValid, string? Error, IBaseRequest? Command);

public class PreviewBulkImportCommandHandler : IRequestHandler<PreviewBulkImportCommand, BulkImportPreviewResult>
{
    private readonly AppDbContext _db;
    public PreviewBulkImportCommandHandler(AppDbContext db) => _db = db;

    public async Task<BulkImportPreviewResult> Handle(PreviewBulkImportCommand request, CancellationToken ct)
    {
        var parsedRows = CsvParser.Parse(request.CsvContent);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<BulkImportRowResult>();

        for (var i = 0; i < parsedRows.Count; i++)
        {
            var validation = await BulkImportRowValidator.ValidateAsync(request.EntityType, parsedRows[i], _db, seenNames, ct);
            results.Add(new BulkImportRowResult(i + 1, validation.IsValid, validation.Error));
        }

        return new BulkImportPreviewResult(results.Count, results.Count(r => r.IsValid), results.Count(r => !r.IsValid), results);
    }
}

public class CommitBulkImportCommandHandler : IRequestHandler<CommitBulkImportCommand, BulkImportCommitResult>
{
    private readonly AppDbContext _db;
    private readonly ISender _sender;
    public CommitBulkImportCommandHandler(AppDbContext db, ISender sender) { _db = db; _sender = sender; }

    public async Task<BulkImportCommitResult> Handle(CommitBulkImportCommand request, CancellationToken ct)
    {
        var parsedRows = CsvParser.Parse(request.CsvContent);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var validations = new List<(RowValidationResult Validation, int RowNumber)>();

        for (var i = 0; i < parsedRows.Count; i++)
        {
            var validation = await BulkImportRowValidator.ValidateAsync(request.EntityType, parsedRows[i], _db, seenNames, ct);
            validations.Add((validation, i + 1));
        }

        var anyInvalid = validations.Any(v => !v.Validation.IsValid);
        var rowResults = validations.Select(v => new BulkImportRowResult(v.RowNumber, v.Validation.IsValid, v.Validation.Error)).ToList();

        if (anyInvalid && !request.PartialCommit)
            return new BulkImportCommitResult(validations.Count, 0, validations.Count, rowResults);

        var committed = 0;
        foreach (var (validation, _) in validations)
        {
            if (!validation.IsValid || validation.Command is null) continue;
            await _sender.Send(validation.Command, ct);
            committed++;
        }

        return new BulkImportCommitResult(validations.Count, committed, validations.Count - committed, rowResults);
    }
}

/// <summary>Per-entity-type row validation + referential lookups, shared by preview (report-only) and
/// commit (builds the Create*Command to dispatch for valid rows).</summary>
internal static class BulkImportRowValidator
{
    public static async Task<RowValidationResult> ValidateAsync(
        BulkImportEntityType entityType, IReadOnlyDictionary<string, string> row, AppDbContext db,
        HashSet<string> seenNamesThisBatch, CancellationToken ct)
    {
        return entityType switch
        {
            BulkImportEntityType.Location => await ValidateLocationAsync(row, db, seenNamesThisBatch, ct),
            BulkImportEntityType.Department => await ValidateDepartmentAsync(row, db, seenNamesThisBatch, ct),
            BulkImportEntityType.Designation => await ValidateDesignationAsync(row, db, seenNamesThisBatch, ct),
            BulkImportEntityType.Employee => await ValidateEmployeeAsync(row, db, seenNamesThisBatch, ct),
            _ => new RowValidationResult(false, "Unknown entity type.", null)
        };
    }

    private static string? Get(IReadOnlyDictionary<string, string> row, string key)
        => row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static async Task<RowValidationResult> ValidateLocationAsync(IReadOnlyDictionary<string, string> row, AppDbContext db, HashSet<string> seen, CancellationToken ct)
    {
        var name = Get(row, "Name");
        if (name is null) return new RowValidationResult(false, "Name is required.", null);
        var key = $"location:{name}";
        if (!seen.Add(key)) return new RowValidationResult(false, $"Duplicate Location name '{name}' within this file.", null);
        if (await db.Locations.AnyAsync(l => l.Name == name, ct)) return new RowValidationResult(false, $"A Location named '{name}' already exists.", null);

        var timeZone = Get(row, "TimeZone") ?? "UTC";
        return new RowValidationResult(true, null, new CreateLocationCommand(name, Get(row, "Address"), timeZone));
    }

    private static async Task<RowValidationResult> ValidateDepartmentAsync(IReadOnlyDictionary<string, string> row, AppDbContext db, HashSet<string> seen, CancellationToken ct)
    {
        var name = Get(row, "Name");
        if (name is null) return new RowValidationResult(false, "Name is required.", null);
        var key = $"department:{name}";
        if (!seen.Add(key)) return new RowValidationResult(false, $"Duplicate Department name '{name}' within this file.", null);
        if (await db.Departments.AnyAsync(d => d.Name == name, ct)) return new RowValidationResult(false, $"A Department named '{name}' already exists.", null);

        Guid? parentId = null;
        var parentName = Get(row, "ParentDepartmentName");
        if (parentName is not null)
        {
            parentId = await db.Departments.Where(d => d.Name == parentName).Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
            if (parentId is null) return new RowValidationResult(false, $"ParentDepartmentName '{parentName}' was not found.", null);
        }

        Guid? headEmployeeId = null;
        var headCode = Get(row, "HeadEmployeeCode");
        if (headCode is not null)
        {
            headEmployeeId = await db.Employees.Where(e => e.EmployeeCode == headCode).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(ct);
            if (headEmployeeId is null) return new RowValidationResult(false, $"HeadEmployeeCode '{headCode}' was not found.", null);
        }

        return new RowValidationResult(true, null, new CreateDepartmentCommand(name, parentId, headEmployeeId));
    }

    private static async Task<RowValidationResult> ValidateDesignationAsync(IReadOnlyDictionary<string, string> row, AppDbContext db, HashSet<string> seen, CancellationToken ct)
    {
        var title = Get(row, "Title");
        if (title is null) return new RowValidationResult(false, "Title is required.", null);
        var key = $"designation:{title}";
        if (!seen.Add(key)) return new RowValidationResult(false, $"Duplicate Designation title '{title}' within this file.", null);
        if (await db.Designations.AnyAsync(d => d.Title == title, ct)) return new RowValidationResult(false, $"A Designation titled '{title}' already exists.", null);

        int? grade = null;
        var gradeRaw = Get(row, "Grade");
        if (gradeRaw is not null)
        {
            if (!int.TryParse(gradeRaw, out var parsedGrade)) return new RowValidationResult(false, $"Grade '{gradeRaw}' is not a valid integer.", null);
            grade = parsedGrade;
        }

        return new RowValidationResult(true, null, new CreateDesignationCommand(title, grade));
    }

    private static async Task<RowValidationResult> ValidateEmployeeAsync(IReadOnlyDictionary<string, string> row, AppDbContext db, HashSet<string> seen, CancellationToken ct)
    {
        var firstName = Get(row, "FirstName");
        var lastName = Get(row, "LastName");
        var joinDateRaw = Get(row, "JoinDate");
        if (firstName is null || lastName is null || joinDateRaw is null)
            return new RowValidationResult(false, "FirstName, LastName, and JoinDate are required.", null);
        if (!DateOnly.TryParse(joinDateRaw, out var joinDate))
            return new RowValidationResult(false, $"JoinDate '{joinDateRaw}' is not a valid date (expected yyyy-MM-dd).", null);

        var workEmail = Get(row, "WorkEmail");
        var key = $"employee:{workEmail ?? $"{firstName}|{lastName}|{joinDateRaw}"}";
        if (!seen.Add(key)) return new RowValidationResult(false, "Duplicate employee row within this file.", null);

        var employmentType = EmploymentType.FullTime;
        var employmentTypeRaw = Get(row, "EmploymentType");
        if (employmentTypeRaw is not null && !Enum.TryParse(employmentTypeRaw, ignoreCase: true, out employmentType))
            return new RowValidationResult(false, $"EmploymentType '{employmentTypeRaw}' is not one of FullTime/PartTime/Contractor.", null);

        Guid? departmentId = null;
        var departmentName = Get(row, "DepartmentName");
        if (departmentName is not null)
        {
            departmentId = await db.Departments.Where(d => d.Name == departmentName).Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
            if (departmentId is null) return new RowValidationResult(false, $"DepartmentName '{departmentName}' was not found.", null);
        }

        Guid? locationId = null;
        var locationName = Get(row, "LocationName");
        if (locationName is not null)
        {
            locationId = await db.Locations.Where(l => l.Name == locationName).Select(l => (Guid?)l.Id).FirstOrDefaultAsync(ct);
            if (locationId is null) return new RowValidationResult(false, $"LocationName '{locationName}' was not found.", null);
        }

        Guid? designationId = null;
        var designationTitle = Get(row, "DesignationName");
        if (designationTitle is not null)
        {
            designationId = await db.Designations.Where(d => d.Title == designationTitle).Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
            if (designationId is null) return new RowValidationResult(false, $"DesignationName '{designationTitle}' was not found.", null);
        }

        Guid? managerId = null;
        var managerCode = Get(row, "ManagerEmployeeCode");
        if (managerCode is not null)
        {
            managerId = await db.Employees.Where(e => e.EmployeeCode == managerCode).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(ct);
            if (managerId is null) return new RowValidationResult(false, $"ManagerEmployeeCode '{managerCode}' was not found.", null);
        }

        return new RowValidationResult(true, null, new CreateEmployeeCommand(
            firstName, lastName, workEmail, Get(row, "PersonalEmail"), Get(row, "Phone"),
            departmentId, locationId, designationId, managerId, employmentType, joinDate));
    }
}
