using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeopleHQ.Domain.Payroll;

namespace PeopleHQ.Infrastructure.Persistence.Configurations;

public class PayComponentConfiguration : IEntityTypeConfiguration<PayComponent>
{
    public void Configure(EntityTypeBuilder<PayComponent> builder)
    {
        builder.Property(c => c.Name).IsRequired().HasMaxLength(150);
        builder.Property(c => c.FormulaJson).HasColumnType("jsonb");
    }
}

public class SalaryStructureConfiguration : IEntityTypeConfiguration<SalaryStructure>
{
    public void Configure(EntityTypeBuilder<SalaryStructure> builder)
    {
        builder.Property(s => s.Name).IsRequired().HasMaxLength(150);
    }
}

public class SalaryStructureComponentConfiguration : IEntityTypeConfiguration<SalaryStructureComponent>
{
    public void Configure(EntityTypeBuilder<SalaryStructureComponent> builder)
    {
        builder.HasKey(c => new { c.SalaryStructureId, c.PayComponentId });
        builder.Property(c => c.DefaultValue).HasColumnType("numeric(14,2)");
    }
}

public class EmployeeSalaryAssignmentConfiguration : IEntityTypeConfiguration<EmployeeSalaryAssignment>
{
    public void Configure(EntityTypeBuilder<EmployeeSalaryAssignment> builder)
    {
        builder.Property(a => a.CtcAnnual).HasColumnType("numeric(14,2)");
        builder.Property(a => a.Currency).HasMaxLength(3);
        builder.HasIndex(a => new { a.EmployeeId, a.EffectiveFrom });
    }
}

public class EmployeeSalaryComponentValueConfiguration : IEntityTypeConfiguration<EmployeeSalaryComponentValue>
{
    public void Configure(EntityTypeBuilder<EmployeeSalaryComponentValue> builder)
    {
        builder.HasKey(v => new { v.AssignmentId, v.PayComponentId });
        builder.Property(v => v.ComputedAmount).HasColumnType("numeric(14,2)");
    }
}

public class StatutorySettingsConfiguration : IEntityTypeConfiguration<StatutorySettings>
{
    public void Configure(EntityTypeBuilder<StatutorySettings> builder)
    {
        builder.Property(s => s.CountryCode).IsRequired().HasMaxLength(2);
        builder.Property(s => s.ConfigJson).HasColumnType("jsonb");
    }
}

public class PtSlabConfiguration : IEntityTypeConfiguration<PtSlab>
{
    public void Configure(EntityTypeBuilder<PtSlab> builder)
    {
        builder.Property(s => s.State).IsRequired().HasMaxLength(100);
        builder.Property(s => s.MinIncome).HasColumnType("numeric(14,2)");
        builder.Property(s => s.MaxIncome).HasColumnType("numeric(14,2)");
        builder.Property(s => s.TaxAmount).HasColumnType("numeric(14,2)");
        builder.HasIndex(s => new { s.TenantId, s.State });
    }
}

public class InvestmentDeclarationConfiguration : IEntityTypeConfiguration<InvestmentDeclaration>
{
    public void Configure(EntityTypeBuilder<InvestmentDeclaration> builder)
    {
        builder.Property(d => d.DeclaredAmount).HasColumnType("numeric(14,2)");
        builder.HasIndex(d => new { d.EmployeeId, d.FinancialYear, d.Section });
    }
}

public class EmployeeTaxRegimeSelectionConfiguration : IEntityTypeConfiguration<EmployeeTaxRegimeSelection>
{
    public void Configure(EntityTypeBuilder<EmployeeTaxRegimeSelection> builder)
    {
        builder.HasIndex(s => new { s.EmployeeId, s.FinancialYear }).IsUnique();
    }
}

public class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> builder)
    {
        builder.HasIndex(r => new { r.TenantId, r.PeriodMonth, r.PeriodYear }).IsUnique();
    }
}

public class PayrollRunItemConfiguration : IEntityTypeConfiguration<PayrollRunItem>
{
    public void Configure(EntityTypeBuilder<PayrollRunItem> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.GrossEarnings).HasColumnType("numeric(14,2)");
        builder.Property(i => i.TotalDeductions).HasColumnType("numeric(14,2)");
        builder.Property(i => i.NetPay).HasColumnType("numeric(14,2)");
        builder.Property(i => i.EmployerPf).HasColumnType("numeric(14,2)");
        builder.Property(i => i.EmployerEsi).HasColumnType("numeric(14,2)");
        builder.Property(i => i.LopDays).HasColumnType("numeric(5,2)");
        builder.HasIndex(i => new { i.PayrollRunId, i.EmployeeId }).IsUnique();
    }
}

public class PayrollRunItemLineConfiguration : IEntityTypeConfiguration<PayrollRunItemLine>
{
    public void Configure(EntityTypeBuilder<PayrollRunItemLine> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Amount).HasColumnType("numeric(14,2)");
        builder.HasIndex(l => l.PayrollRunItemId);
    }
}

public class PayslipConfiguration : IEntityTypeConfiguration<Payslip>
{
    public void Configure(EntityTypeBuilder<Payslip> builder)
    {
        builder.Property(p => p.PdfBlobUrl).IsRequired();
        builder.Property(p => p.YtdGross).HasColumnType("numeric(14,2)");
        builder.Property(p => p.YtdTax).HasColumnType("numeric(14,2)");
        builder.HasIndex(p => new { p.EmployeeId, p.PayrollRunItemId }).IsUnique();
    }
}

public class FullFinalSettlementConfiguration : IEntityTypeConfiguration<FullFinalSettlement>
{
    public void Configure(EntityTypeBuilder<FullFinalSettlement> builder)
    {
        builder.Property(s => s.NetSettlementAmount).HasColumnType("numeric(14,2)");
        builder.HasIndex(s => s.ExitWorkflowRequestId).IsUnique();
    }
}
