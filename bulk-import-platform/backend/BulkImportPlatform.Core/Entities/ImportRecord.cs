using BulkImportPlatform.Core.Enums;

namespace BulkImportPlatform.Core.Entities;

/// <summary>
/// Represents one row of an uploaded spreadsheet ("ImportRecords" table from the proposal).
/// </summary>
public class ImportRecord
{
    public int Id { get; set; }

    public int ImportJobId { get; set; }
    public ImportJob? ImportJob { get; set; }

    public int RowNumber { get; set; }

    // Raw values exactly as read from the spreadsheet (source column name -> raw cell value)
    public string RawDataJson { get; set; } = "{}";

    // Values after mapping + default-value processing (system field name -> value)
    public string MappedDataJson { get; set; } = "{}";

    // Names of fields that had a default value applied, for transparency in the review UI
    public string DefaultsAppliedJson { get; set; } = "[]";

    public RecordStatus Status { get; set; } = RecordStatus.Valid;

    // Id of the Vehicle/Customer row created once this record is committed
    public int? CreatedEntityId { get; set; }

    public List<ValidationResult> ValidationResults { get; set; } = new();

    // RPA push-to-irentosoft outcome for this record (separate stage from local commit above)
    public RpaStatus RpaStatus { get; set; } = RpaStatus.NotAttempted;
    public string? RpaMessage { get; set; }
    public string? RpaEntityReference { get; set; }

    public List<RpaAttempt> RpaAttempts { get; set; } = new();
}
