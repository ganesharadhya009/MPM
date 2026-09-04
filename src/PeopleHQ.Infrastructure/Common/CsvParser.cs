using System.Text;

namespace PeopleHQ.Infrastructure.Common;

/// <summary>
/// Minimal dependency-free RFC 4180-style CSV parser used by BulkImportHandlers (FR-ORG-06). Handles
/// quoted fields (including embedded commas, quotes escaped as "", and embedded newlines) and CRLF/LF line
/// endings. The first row is treated as the header; every subsequent row is returned as a dictionary keyed
/// by (trimmed) header name. This is a deliberate v1 simplification over pulling in a full CSV library —
/// sufficient for the tenant-authored import templates this feature targets.
/// </summary>
public static class CsvParser
{
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> Parse(string csvContent)
    {
        var rows = ParseRows(csvContent);
        if (rows.Count == 0) return Array.Empty<IReadOnlyDictionary<string, string>>();

        var headers = rows[0];
        var result = new List<IReadOnlyDictionary<string, string>>();
        for (var i = 1; i < rows.Count; i++)
        {
            var fields = rows[i];
            if (fields.Count == 1 && fields[0].Length == 0) continue; // skip trailing blank line

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Count; c++)
                dict[headers[c].Trim()] = c < fields.Count ? fields[c] : string.Empty;
            result.Add(dict);
        }
        return result;
    }

    private static List<List<string>> ParseRows(string content)
    {
        var rows = new List<List<string>>();
        var currentRow = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var i = 0;

        while (i < content.Length)
        {
            var ch = content[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"') { field.Append('"'); i += 2; continue; }
                    inQuotes = false;
                    i++;
                    continue;
                }
                field.Append(ch);
                i++;
                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    i++;
                    break;
                case ',':
                    currentRow.Add(field.ToString());
                    field.Clear();
                    i++;
                    break;
                case '\r':
                    i++;
                    break;
                case '\n':
                    currentRow.Add(field.ToString());
                    field.Clear();
                    rows.Add(currentRow);
                    currentRow = new List<string>();
                    i++;
                    break;
                default:
                    field.Append(ch);
                    i++;
                    break;
            }
        }

        if (field.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(field.ToString());
            rows.Add(currentRow);
        }

        return rows;
    }
}
