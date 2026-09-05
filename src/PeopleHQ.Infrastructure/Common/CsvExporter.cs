using System.Reflection;
using System.Text;

namespace PeopleHQ.Infrastructure.Common;

/// <summary>
/// Minimal dependency-free CSV writer (companion to CsvParser) used to satisfy the "export to CSV for every
/// report" requirement (01-modules-functional-spec.md §L) for the flat-list report DTOs. Writes one column
/// per public property via reflection, in declaration order. v1 scope: the Reports module only — wiring CSV
/// export onto every existing list endpoint tenant-wide is a documented follow-up, not done in this pass.
/// XLSX export is likewise a documented follow-up (would need a real spreadsheet library).
/// </summary>
public static class CsvExporter
{
    public static byte[] ToCsvBytes<T>(IEnumerable<T> rows)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', properties.Select(p => Escape(p.Name))));

        foreach (var row in rows)
        {
            var values = properties.Select(p => Escape(p.GetValue(row)?.ToString() ?? string.Empty));
            sb.AppendLine(string.Join(',', values));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>Neutralizes formula characters (=,+,-,@, tab) at the start of a field before quoting/escaping —
    /// otherwise a value like "=HYPERLINK(...)" from user-entered data (an employee name, a leave reason,
    /// etc.) executes as a formula when the exported CSV is opened in Excel/Sheets ("CSV injection").
    /// Prefixing with a leading apostrophe is the standard mitigation and keeps the value's plain-text
    /// meaning intact.</summary>
    private static string Escape(string value)
    {
        if (value.Length > 0 && (value[0] is '=' or '+' or '-' or '@' or '\t'))
            value = "'" + value;

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
