using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PeopleHQ.Infrastructure.Tenancy;

namespace PeopleHQ.Infrastructure.Persistence;

/// <summary>Lets `dotnet ef migrations add` resolve AppDbContext without the full Api host/DI running.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=peoplehq;Username=peoplehq;Password=peoplehq_dev_only")
            .Options;
        return new AppDbContext(options, new TenantContext());
    }
}
