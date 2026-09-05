using System.Net.Sockets;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PeopleHQ.Application.Auth.Interfaces;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Domain.Identity;
using PeopleHQ.Application.Payroll;
using PeopleHQ.Application.Workflow;
using PeopleHQ.Infrastructure.Auditing;
using PeopleHQ.Infrastructure.Common;
using PeopleHQ.Infrastructure.Employees;
using PeopleHQ.Infrastructure.Identity;
using PeopleHQ.Infrastructure.Integrations;
using PeopleHQ.Infrastructure.Notifications;
using PeopleHQ.Infrastructure.Payroll;
using PeopleHQ.Infrastructure.Persistence;
using PeopleHQ.Infrastructure.Tenancy;
using PeopleHQ.Infrastructure.Workflow;

namespace PeopleHQ.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // Tenancy — scoped per request, resolved by TenantResolutionMiddleware.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("Default")));

        services.AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = false; // uniqueness enforced per-tenant, not globally — see AppUserConfiguration
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ITotpService, TotpService>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IManagerCycleValidator, ManagerCycleValidator>();
        services.AddScoped<ICurrentEmployeeResolver, CurrentEmployeeResolver>();
        services.AddScoped<IPermissionChecker, PermissionChecker>();
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        services.AddScoped<IStatutoryCalculator, IndiaStatutoryCalculator>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IEmailSender, LoggingEmailSender>();
        services.AddScoped<IWebhookDispatcher, WebhookDispatcher>();
        services.AddHttpClient(nameof(WebhookDispatcher), client => client.Timeout = TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                // SSRF hardening: never follow a redirect (a target could pass the creation-time SsrfGuard
                // check, then redirect real deliveries to an internal address), and re-resolve + validate
                // the destination IP at the moment of connecting rather than trusting the DNS lookup used at
                // subscription-creation time — a hostname's record can change afterward (DNS rebinding).
                AllowAutoRedirect = false,
                ConnectCallback = async (context, ct) =>
                {
                    var addresses = await System.Net.Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
                    var address = addresses.FirstOrDefault(SsrfGuard.IsPublicAddress)
                        ?? throw new SocketException((int)SocketError.HostNotFound);

                    var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        await socket.ConnectAsync(address, context.DnsEndPoint.Port, ct);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }
            });

        // MediatR scans both Application (command/query contracts) and Infrastructure
        // (handlers) assemblies — see the note in PeopleHQ.Application.DependencyInjection.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
            typeof(Application.Auth.LoginCommand).Assembly,
            typeof(DependencyInjection).Assembly));

        return services;
    }
}
