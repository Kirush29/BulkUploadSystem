using BulkImportPlatform.Core.Enums;

namespace BulkImportPlatform.Core.Entities;

/// <summary>
/// Stores a reusable field-mapping configuration ("MappingProfiles" table from the proposal),
/// e.g. "Manufacturer" (customer column) -> "Make" (system field), plus default-value rules,
/// such as Vehicle Year -> Current Year, Tank Size -> 0, Customer Status -> Active.
/// </summary>
public class MappingProfile
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public ImportType ImportType { get; set; }

    // Dictionary<sourceColumnName, systemFieldName> serialized as JSON
    public string ColumnMappingJson { get; set; } = "{}";

    // Dictionary<systemFieldName, defaultValueExpression> serialized as JSON.
    // Supported expressions: literal values, or the token "CURRENT_YEAR".
    public string DefaultValuesJson { get; set; } = "{}";

    public bool IsSystemDefault { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}
