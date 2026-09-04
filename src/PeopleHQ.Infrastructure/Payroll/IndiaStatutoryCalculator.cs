using System.Text.Json;
using PeopleHQ.Application.Payroll;

namespace PeopleHQ.Infrastructure.Payroll;

/// <summary>
/// India-default statutory engine (01-modules-functional-spec.md §O). Config-driven with sane India defaults
/// when StatutorySettings.ConfigJson omits a key, so a tenant never hits a hard failure for missing config.
///
/// TDS is a deliberately simplified v1 approximation: annual taxable income = CTC - standard deduction (50,000) -
/// verified declarations (capped at 150,000, not section-differentiated) - basic exemption; the FY2024-25 India
/// "New Regime" slabs are applied with the section 87A rebate (nil tax up to 700,000 taxable). Precise multi-regime,
/// multi-section TDS calculation is a documented follow-up, not a Phase 1 requirement per NFR-MAINT-01's
/// pluggable-by-country design intent.
/// </summary>
public class IndiaStatutoryCalculator : IStatutoryCalculator
{
    private const decimal DefaultPfEmployeePercent = 12m;
    private const decimal DefaultPfEmployerPercent = 12m;
    private const decimal DefaultPfWageCeiling = 15000m;
    private const decimal DefaultEsiThreshold = 21000m;
    private const decimal DefaultEsiEmployeePercent = 0.75m;
    private const decimal DefaultEsiEmployerPercent = 3.25m;
    private const decimal StandardDeduction = 50000m;
    private const decimal DeclarationsCap = 150000m;
    private const decimal RebateThreshold = 700000m;

    public StatutoryCalculationResult Calculate(StatutoryCalculationInput input)
    {
        var config = ParseConfig(input.ConfigJson);

        var pfWageBase = Math.Min(input.BasicMonthlyEarnings, GetDecimal(config, "pfWageCeiling", DefaultPfWageCeiling));
        var employeePf = Math.Round(pfWageBase * GetDecimal(config, "pfEmployeePercent", DefaultPfEmployeePercent) / 100m, 2);
        var employerPf = Math.Round(pfWageBase * GetDecimal(config, "pfEmployerPercent", DefaultPfEmployerPercent) / 100m, 2);

        var esiThreshold = GetDecimal(config, "esiThreshold", DefaultEsiThreshold);
        decimal employeeEsi = 0m, employerEsi = 0m;
        if (input.GrossMonthlyEarnings <= esiThreshold)
        {
            employeeEsi = Math.Round(input.GrossMonthlyEarnings * GetDecimal(config, "esiEmployeePercent", DefaultEsiEmployeePercent) / 100m, 2);
            employerEsi = Math.Round(input.GrossMonthlyEarnings * GetDecimal(config, "esiEmployerPercent", DefaultEsiEmployerPercent) / 100m, 2);
        }

        var professionalTax = input.PtSlabs
            .Where(s => input.GrossMonthlyEarnings >= s.MinIncome && input.GrossMonthlyEarnings <= s.MaxIncome)
            .Select(s => s.TaxAmount)
            .FirstOrDefault();

        var tds = CalculateMonthlyTds(input);

        return new StatutoryCalculationResult(employeePf, employerPf, employeeEsi, employerEsi, professionalTax, tds);
    }

    private static decimal CalculateMonthlyTds(StatutoryCalculationInput input)
    {
        var cappedDeclarations = Math.Min(input.VerifiedDeclarationsTotal, DeclarationsCap);
        var taxableIncome = input.CtcAnnual - StandardDeduction - cappedDeclarations;
        if (taxableIncome <= 0) return 0m;
        if (taxableIncome <= RebateThreshold) return 0m; // Section 87A rebate

        var annualTax = CalculateSlabTax(taxableIncome);
        return Math.Round(annualTax / 12m, 2);
    }

    /// <summary>FY2024-25 India "New Regime" slabs.</summary>
    private static decimal CalculateSlabTax(decimal taxableIncome)
    {
        var slabs = new (decimal Upper, decimal Rate)[]
        {
            (300000m, 0m), (600000m, 5m), (900000m, 10m), (1200000m, 15m), (1500000m, 20m), (decimal.MaxValue, 30m)
        };

        decimal tax = 0m, lowerBound = 0m;
        foreach (var (upper, rate) in slabs)
        {
            if (taxableIncome <= lowerBound) break;
            var slabAmount = Math.Min(taxableIncome, upper) - lowerBound;
            tax += slabAmount * rate / 100m;
            lowerBound = upper;
        }
        return tax;
    }

    private static Dictionary<string, JsonElement> ParseConfig(string configJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
        }
        catch (JsonException)
        {
            return new Dictionary<string, JsonElement>();
        }
    }

    private static decimal GetDecimal(Dictionary<string, JsonElement> config, string key, decimal fallback)
        => config.TryGetValue(key, out var value) && value.TryGetDecimal(out var parsed) ? parsed : fallback;
}
