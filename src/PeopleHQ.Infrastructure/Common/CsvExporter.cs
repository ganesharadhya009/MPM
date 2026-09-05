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

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
