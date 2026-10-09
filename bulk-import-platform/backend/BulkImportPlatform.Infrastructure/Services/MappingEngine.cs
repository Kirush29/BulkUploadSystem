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

        // Each system field is suggested for at most one column, and confident matches (exact
        // name, then alias) claim fields before any fuzzy guess - otherwise e.g. "Security
        // Deposit Status" would fuzzy-match onto Status and overwrite the real Status column.
        var claimed = new HashSet<string>();

        foreach (var header in headers)
        {
            var normalized = Normalize(header);

            // 1. Exact match against a system field name
            var exact = systemFields.FirstOrDefault(f => Normalize(f) == normalized);

            // 2. Known alias hint (e.g. "Manufacturer" -> "Make") - only for this import type's fields
            if (exact == null
                && FieldDefinitions.CommonAliasHints.TryGetValue(header.Trim(), out var aliasTarget)
                && systemFields.Contains(aliasTarget))
            {
                exact = aliasTarget;
            }

            if (exact != null && claimed.Add(exact))
            {
                suggestion[header] = exact;
            }
        }

        foreach (var header in headers.Where(h => !suggestion.ContainsKey(h)))
        {
            var normalized = Normalize(header);
            if (normalized.Length == 0) continue;

            // 3. Fuzzy contains match
            var fuzzy = systemFields.FirstOrDefault(f => !claimed.Contains(f) &&
                (normalized.Contains(Normalize(f)) || Normalize(f).Contains(normalized)));
            if (fuzzy != null)
            {
                suggestion[header] = fuzzy;
                claimed.Add(fuzzy);
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
