namespace PeopleHQ.Application.Common.Interfaces;

/// <summary>Resolves the Employee row backing the current authenticated user (Employee.UserId), used by every
/// self-service handler (check-in, leave apply, approvals) that needs "which employee is this?" beyond just UserId.</summary>
public interface ICurrentEmployeeResolver
{
    Task<Guid> GetCurrentEmployeeIdAsync(CancellationToken ct = default);
}
