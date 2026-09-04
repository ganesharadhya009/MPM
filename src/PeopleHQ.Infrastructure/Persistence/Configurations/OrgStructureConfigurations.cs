using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeopleHQ.Domain.OrgStructure;

namespace PeopleHQ.Infrastructure.Persistence.Configurations;

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.Property(l => l.Name).IsRequired().HasMaxLength(200);
        builder.Property(l => l.TimeZone).IsRequired().HasMaxLength(64);
    }
}

public class HolidayCalendarConfiguration : IEntityTypeConfiguration<HolidayCalendar>
{
    public void Configure(EntityTypeBuilder<HolidayCalendar> builder)
    {
        builder.Property(h => h.Name).IsRequired().HasMaxLength(200);
    }
}

public class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.Property(h => h.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(h => new { h.HolidayCalendarId, h.Date });
    }
}

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.Property(d => d.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(d => d.ParentDepartmentId);
    }
}

public class DesignationConfiguration : IEntityTypeConfiguration<Designation>
{
    public void Configure(EntityTypeBuilder<Designation> builder)
    {
        builder.Property(d => d.Title).IsRequired().HasMaxLength(150);
    }
}

public class EmployeePositionHistoryConfiguration : IEntityTypeConfiguration<EmployeePositionHistory>
{
    public void Configure(EntityTypeBuilder<EmployeePositionHistory> builder)
    {
        builder.HasIndex(h => new { h.EmployeeId, h.EffectiveFrom });
    }
}
