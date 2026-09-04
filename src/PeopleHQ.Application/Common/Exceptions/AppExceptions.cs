namespace PeopleHQ.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key) : base($"{entityName} ({key}) was not found.") { }
}

public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors) : base("One or more validation errors occurred.")
        => Errors = errors;

    public ValidationException(string field, string message) : this(new Dictionary<string, string[]> { [field] = new[] { message } }) { }
}

/// <summary>e.g. deleting a Department with active employees (FR-ORG-01) — mapped to 409.</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

/// <summary>e.g. acting on someone else's request, or a permission edge-case not covered by [RequirePermission] alone — mapped to 403.</summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}
