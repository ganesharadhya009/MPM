using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeopleHQ.Domain.Attendance;

namespace PeopleHQ.Infrastructure.Persistence.Configurations;

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
    }
}

public class ShiftAssignmentConfiguration : IEntityTypeConfiguration<ShiftAssignment>
{
    public void Configure(EntityTypeBuilder<ShiftAssignment> builder)
    {
        builder.HasIndex(a => new { a.EmployeeId, a.EffectiveFrom });
    }
}

public class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.Property(a => a.OvertimeHours).HasColumnType("numeric(5,2)");
        builder.HasIndex(a => new { a.TenantId, a.EmployeeId, a.Date }).IsUnique();
    }
}

public class AttendanceRegularizationRequestConfiguration : IEntityTypeConfiguration<AttendanceRegularizationRequest>
{
    public void Configure(EntityTypeBuilder<AttendanceRegularizationRequest> builder)
    {
        builder.Property(r => r.Reason).IsRequired();
        builder.HasIndex(r => r.AttendanceRecordId);
    }
}
