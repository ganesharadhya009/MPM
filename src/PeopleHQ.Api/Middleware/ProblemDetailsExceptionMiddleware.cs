using System.Text.Json;
using PeopleHQ.Application.Common.Exceptions;

namespace PeopleHQ.Api.Middleware;

/// <summary>Every error response is RFC 7807 application/problem+json (03-api-design.md) — no controller shapes its own error body.</summary>
public class ProblemDetailsExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsExceptionMiddleware> _logger;

    public ProblemDetailsExceptionMiddleware(RequestDelegate next, ILogger<ProblemDetailsExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NotFoundException ex)
        {
            await WriteProblem(context, 404, "https://peoplehq.app/errors/not-found", "Resource not found", ex.Message);
        }
        catch (ValidationException ex)
        {
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                type = "https://peoplehq.app/errors/validation",
                title = "Validation failed",
                status = 400,
                detail = ex.Message,
                errors = ex.Errors,
            }));
        }
        catch (ConflictException ex)
        {
            await WriteProblem(context, 409, "https://peoplehq.app/errors/conflict", "Conflict", ex.Message);
        }
        catch (ForbiddenException ex)
        {
            await WriteProblem(context, 403, "https://peoplehq.app/errors/forbidden", "Forbidden", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteProblem(context, 500, "https://peoplehq.app/errors/internal", "An unexpected error occurred", ex.Message);
        }
    }

    private static async Task WriteProblem(HttpContext context, int status, string type, string title, string detail)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { type, title, status, detail }));
    }
}
