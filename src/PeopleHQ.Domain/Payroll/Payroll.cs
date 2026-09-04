using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.Payroll;

public enum PayComponentType { Earning, Deduction }
public enum PayComponentAmountType { Flat, PercentOfBasic, PercentOfCTC, Formula }
public enum PayType { Salaried, Hourly, Contract }
public enum DeclarationStatus { Declared, ProofSubmitted, Verified, Rejected }
public enum PayrollRunStatus { Draft, Computed, PendingApproval, Approved, Locked, Paid }
public enum PaymentStatus { Pending, Paid, Failed }

/// <summary>Tenant-defined building blocks. India-sensible defaults seeded, all tenant-editable, none hardcoded (01-modules-functional-spec.md §O).</summary>
public class PayComponent : TenantOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public PayComponentType ComponentType { get; set; }
    public PayComponentAmountType AmountType { get; set; }
    public string? FormulaJson { get; set; } // used when AmountType == Formula
    public bool IsTaxable { get; set; }
    public bool IsStatutory { get; set; } // flags PF/ESI-type system components
    public int SortOrder { get; set; }
}

public class SalaryStructure : TenantOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class SalaryStructureComponent
{
    public Guid SalaryStructureId { get; set; }
    public Guid PayComponentId { get; set; }
    public decimal DefaultValue { get; set; } // interpreted per the component's AmountType
    public int SortOrder { get; set; }
}

/// <summary>Effective-dated — a revision inserts a new row, never overwrites (FR-PAY-03). Salary history is a compliance requirement.</summary>
public class EmployeeSalaryAssignment : TenantOwnedEntity
{
    public Guid EmployeeId { get; set; }
    public Guid SalaryStructureId { get; set; }
    public PayType PayType { get; set; } = PayType.Salaried;
    public decimal CtcAnnual { get; set; }
    public string Currency { get; set; } = Common.Money.DefaultCurrency;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

/// <summary>Snapshot of each component's resolved value as of the assignment's effective date.</summary>
public class EmployeeSalaryComponentValue
{
    public Guid AssignmentId { get; set; }
    public Guid PayComponentId { get; set; }
    public decimal ComputedAmount { get; set; }
}

/// <summary>Pluggable-by-country extension point (NFR-MAINT-01). CountryCode defaults to IN for Phase 1.</summary>
public class StatutorySettings : TenantOwnedEntity
{
    public string CountryCode { get; set; } = "IN";
    /// <summary>PF employee/employer %, PF wage ceiling, ESI threshold + %, TDS regime defaults.</summary>
    public string ConfigJson { get; set; } = "{}";
}

/// <summary>India Professional Tax, state-wise slabs.</summary>
public class PtSlab : TenantOwnedEntity
{
    public string State { get; set; } = string.Empty;
    public decimal MinIncome { get; set; }
    public decimal MaxIncome { get; set; }
    public decimal TaxAmount { get; set; }
}

public class InvestmentDeclaration : TenantOwnedEntity
{
    public Guid EmployeeId { get; set; }
    public string FinancialYear { get; set; } = string.Empty; // e.g. "2026-27"
    public string Section { get; set; } = string.Empty; // 80C / 80D / HRA / ...
    public decimal DeclaredAmount { get; set; }
    public string? ProofBlobUrl { get; set; }
    public DeclarationStatus Status { get; set; } = DeclarationStatus.Declared;
    public Guid? VerifiedBy { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
}

/// <summary>Employee's chosen tax regime (India: Old vs New), revisable once per financial year per statutory rule.</summary>
public class EmployeeTaxRegimeSelection : TenantOwnedEntity
{
    public Guid EmployeeId { get; set; }
    public string FinancialYear { get; set; } = string.Empty;
    public string Regime { get; set; } = "New"; // Old / New
}

public class PayrollRun : TenantOwnedEntity
{
    public int PeriodMonth { get; set; }
    public int PeriodYear { get; set; }
    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;
    public Guid? WorkflowRequestId { get; set; }
    public DateTime? LockedAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
}

public class PayrollRunItem : BaseEntity
{
    public Guid PayrollRunId { get; set; }
    public Guid EmployeeId { get; set; }
    public decimal GrossEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }
    public decimal EmployerPf { get; set; }
    public decimal EmployerEsi { get; set; }
    public decimal LopDays { get; set; } // loss-of-pay days from Attendance/Leave
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public Guid? OverriddenBy { get; set; }
    public string? OverrideReason { get; set; } // required if any line under it is a manual override
}

public class PayrollRunItemLine : BaseEntity
{
    public Guid PayrollRunItemId { get; set; }
    public Guid PayComponentId { get; set; }
    public decimal Amount { get; set; }
    public bool IsManualOverride { get; set; }
}

/// <summary>Immutable once generated (NFR-COMP-07) — a correction produces a new payslip via an adjustment run.</summary>
public class Payslip : TenantOwnedEntity
{
    public Guid EmployeeId { get; set; }
    public Guid PayrollRunItemId { get; set; }
    public string PdfBlobUrl { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public decimal YtdGross { get; set; }
    public decimal YtdTax { get; set; }
}

public class FullFinalSettlement : TenantOwnedEntity
{
    public Guid EmployeeId { get; set; }
    public Guid ExitWorkflowRequestId { get; set; }
    public DateTime ComputedAtUtc { get; set; } = DateTime.UtcNow;
    public decimal NetSettlementAmount { get; set; }
    public Guid? PayslipId { get; set; }
}
