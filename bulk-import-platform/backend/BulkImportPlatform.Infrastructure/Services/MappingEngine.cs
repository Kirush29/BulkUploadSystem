using BulkImportPlatform.Core;
using BulkImportPlatform.Core.Enums;
using BulkImportPlatform.Core.Interfaces;

namespace BulkImportPlatform.Infrastructure.Services;

/// <summary>
/// Mapping Engine (Business Layer) - aligns customer-provided spreadsheet columns with
/// system fields, per section 6 (Data Mapping and Transformation) of the proposal.
/// </summary>
public class MappingEngine : IMappingEngine
{
    public Dictionary<string, string> SuggestMapping(List<string> headers, ImportType type)
    {
        var systemFields = FieldDefinitions.For(type).Select(f => f.SystemField).ToList();
        var suggestion = new Dictionary<string, string>();

        foreach (var header in headers)
        {
            var normalized = Normalize(header);

            // 1. Exact match against a system field name
            var exact = systemFields.FirstOrDefault(f => Normalize(f) == normalized);
            if (exact != null)
            {
                suggestion[header] = exact;
                continue;
            }

            // 2. Known alias hint (e.g. "Manufacturer" -> "Make")
            if (FieldDefinitions.CommonAliasHints.TryGetValue(header.Trim(), out var aliasTarget))
            {
                suggestion[header] = aliasTarget;
                continue;
            }

            // 3. Fuzzy contains match
            var fuzzy = systemFields.FirstOrDefault(f =>
                normalized.Contains(Normalize(f)) || Normalize(f).Contains(normalized));
            if (fuzzy != null)
            {
                suggestion[header] = fuzzy;
            }
            // Unmatched columns are left out of the suggestion; the user maps them manually
            // or leaves them unmapped (ignored during import).
        }

        return suggestion;
    }

    public List<Dictionary<string, string?>> ApplyMapping(
        List<Dictionary<string, string?>> rawRows,
        Dictionary<string, string> columnMapping,
        ImportType type)
    {
        var validSystemFields = FieldDefinitions.For(type).Select(f => f.SystemField).ToHashSet();
        var mappedRows = new List<Dictionary<string, string?>>();

        foreach (var rawRow in rawRows)
        {
            var mappedRow = new Dictionary<string, string?>();
            foreach (var (sourceColumn, systemField) in columnMapping)
            {
                if (!validSystemFields.Contains(systemField)) continue;
                rawRow.TryGetValue(sourceColumn, out var value);
                mappedRow[systemField] = value;
            }
            mappedRows.Add(mappedRow);
        }

        return mappedRows;
    }

    private static string Normalize(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
