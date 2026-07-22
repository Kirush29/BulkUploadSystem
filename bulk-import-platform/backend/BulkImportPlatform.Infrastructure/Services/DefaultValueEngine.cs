using BulkImportPlatform.Core;
using BulkImportPlatform.Core.Enums;
using BulkImportPlatform.Core.Interfaces;

namespace BulkImportPlatform.Infrastructure.Services;

/// <summary>
/// Default Value Engine (Business Layer) - assigns default values to missing fields to
/// maximize import success rates, per section 7 of the proposal
/// (e.g. Vehicle Year -> Current Year, Tank Size -> 0, Customer Status -> Active).
/// The system clearly indicates (via the returned list) where a default was applied,
/// which the API surfaces back to the review/preview screen.
/// </summary>
public class DefaultValueEngine : IDefaultValueEngine
{
    public List<string> ApplyDefaults(
        Dictionary<string, string?> mappedRow,
        Dictionary<string, string> configuredDefaults,
        ImportType type)
    {
        var builtIns = type == ImportType.Vehicle
            ? FieldDefinitions.BuiltInDefaultsVehicle
            : FieldDefinitions.BuiltInDefaultsCustomer;

        // Configured (user-supplied) defaults take precedence over built-in ones.
        var effectiveDefaults = new Dictionary<string, string>(builtIns, StringComparer.OrdinalIgnoreCase);
        foreach (var (field, value) in configuredDefaults)
        {
            effectiveDefaults[field] = value;
        }

        var applied = new List<string>();

        foreach (var (field, defaultExpression) in effectiveDefaults)
        {
            var isMissing = !mappedRow.TryGetValue(field, out var current) || string.IsNullOrWhiteSpace(current);
            if (!isMissing) continue;

            mappedRow[field] = Resolve(defaultExpression);
            applied.Add(field);
        }

        return applied;
    }

    private static string Resolve(string expression) =>
        expression.Equals("CURRENT_YEAR", StringComparison.OrdinalIgnoreCase)
            ? DateTime.UtcNow.Year.ToString()
            : expression;
}
