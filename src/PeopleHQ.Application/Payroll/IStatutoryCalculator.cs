namespace PeopleHQ.Application.Payroll;

/// <summary>Pluggable-by-country extension point (NFR-MAINT-01, mirrors StatutorySettings.CountryCode) — Phase 1
/// ships only IndiaStatutoryCalculator; a future country registers its own IStatutoryCalculator implementation
/// and the payroll run handler selects by StatutorySettings.CountryCode instead of switching on it inline.</summary>
public interface IStatutoryCalculator
{
    StatutoryCalculationResult Calculate(StatutoryCalculationInput input);
}

public record PtSlabInput(decimal MinIncome, decimal MaxIncome, decimal TaxAmount);

public record StatutoryCalculationInput(
    decimal GrossMonthlyEarnings,
    decimal BasicMonthlyEarnings,
    decimal CtcAnnual,
    /// <summary>Sum of this employee's Verified InvestmentDeclaration amounts for the financial year — a v1
    /// simplification that does not separate declarations by section (80C/80D/HRA/...) for TDS purposes.</summary>
    decimal VerifiedDeclarationsTotal,
    string TaxRegime,
    /// <summary>StatutorySettings.ConfigJson — expected keys: pfEmployeePercent, pfEmployerPercent, pfWageCeiling,
    /// esiThreshold, esiEmployeePercent, esiEmployerPercent. Missing/unparseable keys fall back to India defaults.</summary>
    string ConfigJson,
    IReadOnlyList<PtSlabInput> PtSlabs);

public record StatutoryCalculationResult(decimal EmployeePf, decimal EmployerPf, decimal EmployeeEsi, decimal EmployerEsi, decimal ProfessionalTax, decimal Tds);
