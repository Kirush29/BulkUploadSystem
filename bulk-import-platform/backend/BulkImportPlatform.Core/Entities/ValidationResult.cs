using BulkImportPlatform.Core.Enums;

namespace BulkImportPlatform.Core.Entities;

/// <summary>
/// Stores a single validation error or warning for an import record
/// ("ValidationResults" table from the proposal).
/// </summary>
public class ValidationResult
{
    public int Id { get; set; }

    public int ImportRecordId { get; set; }
    public ImportRecord? ImportRecord { get; set; }

    public ValidationSeverity Severity { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string RuleCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
