using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeopleHQ.Domain.Leave;

namespace PeopleHQ.Infrastructure.Persistence.Configurations;

public class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> builder)
    {
        builder.Property(t => t.Name).IsRequired().HasMaxLength(100);
        builder.Property(t => t.AnnualEntitlement).HasColumnType("numeric(6,2)");
        builder.Property(t => t.CarryForwardCap).HasColumnType("numeric(6,2)");
    }
}

public class LeavePolicyConfiguration : IEntityTypeConfiguration<LeavePolicy>
{
    public void Configure(EntityTypeBuilder<LeavePolicy> builder)
    {
        builder.Property(p => p.Name).IsRequired().HasMaxLength(150);
    }
}

public class LeaveTypePolicyRuleConfiguration : IEntityTypeConfiguration<LeaveTypePolicyRule>
{
    public void Configure(EntityTypeBuilder<LeaveTypePolicyRule> builder)
    {
        builder.HasKey(r => new { r.PolicyId, r.LeaveTypeId });
        builder.Property(r => r.EntitlementOverride).HasColumnType("numeric(6,2)");
    }
}

public class EmployeeLeavePolicyConfiguration : IEntityTypeConfiguration<EmployeeLeavePolicy>
{
    public void Configure(EntityTypeBuilder<EmployeeLeavePolicy> builder)
    {
        builder.HasKey(e => new { e.EmployeeId, e.PolicyId });
    }
}

public class LeaveBalanceConfiguration : IEntityTypeConfiguration<LeaveBalance>
{
    public void Configure(EntityTypeBuilder<LeaveBalance> builder)
    {
        builder.HasKey(b => new { b.EmployeeId, b.LeaveTypeId, b.Year });
        builder.Property(b => b.Accrued).HasColumnType("numeric(6,2)");
        builder.Property(b => b.Used).HasColumnType("numeric(6,2)");
        builder.Property(b => b.CarriedForward).HasColumnType("numeric(6,2)");
        builder.Property(b => b.Reserved).HasColumnType("numeric(6,2)");
    }
}

public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.HasIndex(r => new { r.TenantId, r.EmployeeId, r.Status });
    }
}

public class LeaveBlackoutPeriodConfiguration : IEntityTypeConfiguration<LeaveBlackoutPeriod>
{
    public void Configure(EntityTypeBuilder<LeaveBlackoutPeriod> builder)
    {
        builder.Property(b => b.Name).IsRequired().HasMaxLength(150);
    }
}
