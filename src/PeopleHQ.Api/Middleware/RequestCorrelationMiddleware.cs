using PeopleHQ.Application.Common.Interfaces;
using Serilog.Context;

namespace PeopleHQ.Api.Middleware;

/// <summary>Pushes TenantId + RequestId into every log line for the duration of the request (00-overview.md §2 observability).</summary>
public class RequestCorrelationMiddleware
{
    private readonly RequestDelegate _next;
    public RequestCorrelationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        using (LogContext.PushProperty("RequestId", context.TraceIdentifier))
        using (LogContext.PushProperty("TenantId", tenantContext.HasTenant ? tenantContext.TenantId.ToString() : "none"))
        {
            await _next(context);
        }
    }
}
