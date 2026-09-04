using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Domain.Employees;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Common;

public class CurrentEmployeeResolver : ICurrentEmployeeResolver
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private Guid? _cachedEmployeeId;

    public CurrentEmployeeResolver(AppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<Guid> GetCurrentEmployeeIdAsync(CancellationToken ct = default)
    {
        if (_cachedEmployeeId is not null) return _cachedEmployeeId.Value;

        var userId = _currentUser.UserId ?? throw new ForbiddenException("No authenticated user.");
        var employeeId = await _db.Employees.Where(e => e.UserId == userId).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Employee), userId);

        _cachedEmployeeId = employeeId;
        return employeeId;
    }
}
