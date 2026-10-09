using BulkImportPlatform.Core.Enums;

namespace BulkImportPlatform.Core.DTOs;

// ---------- Upload (Step 1-2: Upload Excel File, Parse Spreadsheet Data) ----------

public class UploadResultDto
{
    public int ImportJobId { get; set; }
    public int ClientAccountId { get; set; }
    public string ClientAccountName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public List<string> DetectedHeaders { get; set; } = new();
    public List<Dictionary<string, string?>> SampleRows { get; set; } = new();
    public int TotalRows { get; set; }
    public Dictionary<string, string> SuggestedMapping { get; set; } = new();
    public List<FieldDefinitionDto> SystemFields { get; set; } = new();
}

public class FieldDefinitionDto
{
    public string SystemField { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public string DataType { get; set; } = "string";
}

// ---------- Mapping (Step 3: Field Mapping) ----------

public class SaveMappingRequestDto
{
    public string? ProfileName { get; set; }
    public Dictionary<string, string> ColumnMapping { get; set; } = new(); // sourceColumn -> systemField
    public Dictionary<string, string> DefaultValues { get; set; } = new(); // systemField -> value / CURRENT_YEAR
    public bool SaveAsReusableProfile { get; set; }
}

// ---------- Validation / Preview (Step 4-6) ----------

public class ValidationResultDto
{
    public ValidationSeverity Severity { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string RuleCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ImportRecordDto
{
    public int Id { get; set; }
    public int RowNumber { get; set; }
    public Dictionary<string, string?> MappedData { get; set; } = new();
    public List<string> DefaultsApplied { get; set; } = new();
    public RecordStatus Status { get; set; }
    public List<ValidationResultDto> ValidationResults { get; set; } = new();
}

public class ValidationSummaryDto
{
    public int ImportJobId { get; set; }
    public ImportJobStatus Status { get; set; }
    public int TotalRecords { get; set; }
    public int ValidRecords { get; set; }
    public int WarningRecords { get; set; }
    public int ErrorRecords { get; set; }
    public List<ImportRecordDto> Records { get; set; } = new();
}

// ---------- Commit / Result (Step 7-9: Bulk Import, Result, Audit) ----------

public class ImportSummaryReportDto
{
    public int ImportJobId { get; set; }
    public ImportJobStatus Status { get; set; }
    public int TotalRecords { get; set; }
    public int ImportedRecords { get; set; }
    public int FailedRecords { get; set; }
    public int WarningRecords { get; set; }
    public DateTime? CompletedAt { get; set; }
}

// ---------- Upload History (Reporting Requirements) ----------

public class UploadHistoryItemDto
{
    public int ImportJobId { get; set; }
    public DateTime UploadedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? ClientAccountName { get; set; }
    public ImportType ImportType { get; set; }
    public ImportJobStatus Status { get; set; }
    public int TotalRecords { get; set; }
    public int ImportedRecords { get; set; }
    public int FailedRecords { get; set; }
    public int WarningRecords { get; set; }
}

// ---------- Dashboard ----------

public class DashboardSummaryDto
{
    public int TotalImportJobs { get; set; }
    public int TotalVehiclesImported { get; set; }
    public int TotalCustomersImported { get; set; }
    public int JobsInProgress { get; set; }
    public int JobsCompletedToday { get; set; }
    public List<UploadHistoryItemDto> RecentJobs { get; set; } = new();
}

// ---------- Audit ----------

public class AuditLogDto
{
    public int Id { get; set; }
    public int? ImportJobId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Details { get; set; }
}

// ---------- Client Accounts (irentosoft login per client) ----------

// Never carries a password field - GET responses must never round-trip credentials.
public class ClientAccountDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IrentosoftUsername { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public class CreateClientAccountRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string IrentosoftUsername { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class UpdateClientAccountRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string IrentosoftUsername { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class ResetClientAccountPasswordRequestDto
{
    public string Password { get; set; } = string.Empty;
}

// ---------- RPA push-to-irentosoft (Step 10, after local Bulk Import) ----------

public class RpaRunRequestDto
{
    // No ClientAccountId here - the job already has one, assigned at upload time, since
    // duplicate-checking during validation was already scoped against that specific account.
    public string? OwningLocation { get; set; }
    public string? CurrentLocation { get; set; }
    public RpaRunOptionsDto? Options { get; set; }
}

public class RpaRetryRequestDto
{
    public List<int> ImportRecordIds { get; set; } = new();
    public RpaRunOptionsDto? Options { get; set; }
}

public class UpdateRecordAndRetryRequestDto
{
    // Only the fields being corrected (e.g. { "VehicleType": "SUV", "Model": "Rogue" }) -
    // merged into the record's existing mapped data, everything else is left untouched.
    public Dictionary<string, string?> FieldUpdates { get; set; } = new();
    public RpaRunOptionsDto? Options { get; set; }
}

public class RpaRunOptionsDto
{
    public bool Headed { get; set; } = false;
    public bool CaptureScreenshotsOnFailure { get; set; } = true;
    // Vehicle pushes only: before entering any vehicle, add every Make/Model the batch uses
    // that the client's irentosoft account doesn't have yet (Settings > Asset > Asset Make /
    // Asset Model). Opt-in - intended for new accounts with an empty catalog. Existing entries
    // are never re-added (case-insensitive match against the live dropdown lists).
    public bool CreateMissingMakesAndModels { get; set; } = false;

    // Reservation pushes only: which source-file Status values to push (case-insensitive, e.g.
    // ["reserved"]; "" = blank Status). Null/empty pushes every row. Applies to a full run only,
    // not Retry / Edit & Retry. Rows with other statuses are left untouched (NotAttempted), so a
    // later run with a different selection can still push them.
    public List<string>? ReservationStatuses { get; set; }

    // Reservation pushes only: how many booking API calls run at once. Reservations go to
    // irentosoft's API directly rather than through its web forms, so this is the main speed
    // lever; kept low by default since it is a live production system. Clamped to 1-8.
    public int MaxParallelRequests { get; set; } = 4;
}

public class RpaRunSummaryDto
{
    public int ImportJobId { get; set; }
    public ImportJobStatus Status { get; set; }
    public int TotalEligible { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public List<RpaRecordResultDto> Records { get; set; } = new();
}

public class RpaRecordResultDto
{
    public int ImportRecordId { get; set; }
    public int RowNumber { get; set; }
    public RpaStatus RpaStatus { get; set; }
    public string? RpaMessage { get; set; }
    public int AttemptCount { get; set; }
    // Path to a full-page screenshot taken at the moment of the most recent failed attempt, if
    // any - shows the real dropdown options that were on screen when a match wasn't found.
    public string? LastScreenshotPath { get; set; }
    // Current mapped field data, so a failed row can be edited (see UpdateRecordAndRetryAsync)
    // without a separate lookup call.
    public Dictionary<string, string?> MappedData { get; set; } = new();
}
