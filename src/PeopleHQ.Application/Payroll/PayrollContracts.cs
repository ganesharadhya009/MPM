using MediatR;
using PeopleHQ.Application.Common;
using PeopleHQ.Domain.Payroll;

namespace PeopleHQ.Application.Payroll;

// --- Pay Components ---
public record CreatePayComponentCommand(string Name, PayComponentType ComponentType, PayComponentAmountType AmountType, string? FormulaJson, bool IsTaxable, bool IsStatutory, int SortOrder) : IRequest<Guid>;
public record UpdatePayComponentCommand(Guid Id, string Name, PayComponentAmountType AmountType, string? FormulaJson, bool IsTaxable, int SortOrder) : IRequest;
public record DeletePayComponentCommand(Guid Id) : IRequest;
public record GetPayComponentsQuery : IRequest<IReadOnlyList<PayComponentDto>>;
public record PayComponentDto(Guid Id, string Name, PayComponentType ComponentType, PayComponentAmountType AmountType, string? FormulaJson, bool IsTaxable, bool IsStatutory, int SortOrder);

// --- Salary Structures ---
public record StructureComponentInput(Guid PayComponentId, decimal DefaultValue, int SortOrder);
public record CreateSalaryStructureCommand(string Name, string? Description, IReadOnlyList<StructureComponentInput> Components) : IRequest<Guid>;
public record UpdateSalaryStructureCommand(Guid Id, string Name, string? Description, IReadOnlyList<StructureComponentInput> Components) : IRequest;
public record DeleteSalaryStructureCommand(Guid Id) : IRequest;
public record GetSalaryStructuresQuery : IRequest<IReadOnlyList<SalaryStructureDto>>;
public record SalaryStructureComponentDto(Guid PayComponentId, decimal DefaultValue, int SortOrder);
public record SalaryStructureDto(Guid Id, string Name, string? Description, IReadOnlyList<SalaryStructureComponentDto> Components);

// --- Employee Salary Assignment (FR-PAY-03: effective-dated, never overwritten) ---
public record AssignSalaryCommand(Guid EmployeeId, Guid SalaryStructureId, PayType PayType, decimal CtcAnnual, string Currency, DateOnly EffectiveFrom) : IRequest<Guid>;
public record GetEmployeeSalaryHistoryQuery(Guid EmployeeId) : IRequest<IReadOnlyList<SalaryAssignmentDto>>;
public record SalaryComponentValueDto(Guid PayComponentId, decimal ComputedAmount);
public record SalaryAssignmentDto(Guid Id, Guid EmployeeId, Guid SalaryStructureId, PayType PayType, decimal CtcAnnual, string Currency,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo, IReadOnlyList<SalaryComponentValueDto> ComponentValues);

// --- Statutory Settings / PT Slabs ---
public record UpsertStatutorySettingsCommand(string CountryCode, string ConfigJson) : IRequest;
public record GetStatutorySettingsQuery : IRequest<StatutorySettingsDto?>;
public record StatutorySettingsDto(Guid Id, string CountryCode, string ConfigJson);

public record CreatePtSlabCommand(string State, decimal MinIncome, decimal MaxIncome, decimal TaxAmount) : IRequest<Guid>;
public record DeletePtSlabCommand(Guid Id) : IRequest;
public record GetPtSlabsQuery(string? State = null) : IRequest<IReadOnlyList<PtSlabDto>>;
public record PtSlabDto(Guid Id, string State, decimal MinIncome, decimal MaxIncome, decimal TaxAmount);

// --- Investment Declarations & Tax Regime (employee self-service) ---
public record CreateInvestmentDeclarationCommand(string FinancialYear, string Section, decimal DeclaredAmount, string? ProofBlobUrl) : IRequest<Guid>;
public record VerifyInvestmentDeclarationCommand(Guid Id, DeclarationStatus Status) : IRequest;
public record GetInvestmentDeclarationsQuery(Guid? EmployeeId = null, string? FinancialYear = null) : IRequest<IReadOnlyList<InvestmentDeclarationDto>>;
public record InvestmentDeclarationDto(Guid Id, Guid EmployeeId, string FinancialYear, string Section, decimal DeclaredAmount, string? ProofBlobUrl, DeclarationStatus Status);

public record SelectTaxRegimeCommand(string FinancialYear, string Regime) : IRequest;
public record GetTaxRegimeSelectionQuery(Guid EmployeeId, string FinancialYear) : IRequest<TaxRegimeSelectionDto?>;
public record TaxRegimeSelectionDto(Guid EmployeeId, string FinancialYear, string Regime);

// --- Payroll Run lifecycle (FR-PAY), routed through the generic Workflow engine ---
public record CreatePayrollRunCommand(int PeriodMonth, int PeriodYear) : IRequest<Guid>;
public record ComputePayrollRunCommand(Guid PayrollRunId) : IRequest;
public record SubmitPayrollRunForApprovalCommand(Guid PayrollRunId) : IRequest;
public record LockPayrollRunCommand(Guid PayrollRunId) : IRequest;
public record MarkPayrollRunPaidCommand(Guid PayrollRunId) : IRequest;
public record OverridePayrollRunItemLineCommand(Guid PayrollRunItemId, Guid PayComponentId, decimal Amount, string OverrideReason) : IRequest;

public record GetPayrollRunsQuery(int? PeriodYear = null) : IRequest<IReadOnlyList<PayrollRunSummaryDto>>;
public record PayrollRunSummaryDto(Guid Id, int PeriodMonth, int PeriodYear, PayrollRunStatus Status, Guid? WorkflowRequestId, int EmployeeCount, decimal TotalNetPay);

public record GetPayrollRunItemsQuery(Guid PayrollRunId) : IRequest<IReadOnlyList<PayrollRunItemDto>>;
public record PayrollRunItemLineDto(Guid PayComponentId, decimal Amount, bool IsManualOverride);
public record PayrollRunItemDto(Guid Id, Guid EmployeeId, decimal GrossEarnings, decimal TotalDeductions, decimal NetPay,
    decimal EmployerPf, decimal EmployerEsi, decimal LopDays, PaymentStatus PaymentStatus, IReadOnlyList<PayrollRunItemLineDto> Lines);

// --- Payslips ---
public record GeneratePayslipsCommand(Guid PayrollRunId) : IRequest;
public record GetPayslipsQuery(Guid? EmployeeId = null) : IRequest<IReadOnlyList<PayslipDto>>;
public record PayslipDto(Guid Id, Guid EmployeeId, Guid PayrollRunItemId, string PdfBlobUrl, DateTime GeneratedAtUtc, decimal YtdGross, decimal YtdTax);

// --- Full & Final Settlement ---
public record ComputeFullFinalSettlementCommand(Guid EmployeeId, Guid ExitWorkflowRequestId) : IRequest<Guid>;
public record GetFullFinalSettlementQuery(Guid EmployeeId) : IRequest<FullFinalSettlementDto?>;
public record FullFinalSettlementDto(Guid Id, Guid EmployeeId, Guid ExitWorkflowRequestId, DateTime ComputedAtUtc, decimal NetSettlementAmount, Guid? PayslipId);
