using BulkImportPlatform.Core.Enums;

namespace BulkImportPlatform.Core.Entities;

/// <summary>
/// Represents a single upload/import session ("ImportJobs" table from the proposal's
/// Proposed Database Components section). One row is created per uploaded file.
/// </summary>
public class ImportJob
{
    public int Id { get; set; }

    public ImportType ImportType { get; set; }
    public ImportJobStatus Status { get; set; } = ImportJobStatus.Uploaded;

    public string FileName { get; set; } = string.Empty;
    public string StoredFilePath { get; set; } = string.Empty;

    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Column headers detected in the uploaded spreadsheet, stored as JSON string array
    public string DetectedHeadersJson { get; set; } = "[]";

    // The mapping profile actually used for this job (source column -> system field)
    public int? MappingProfileId { get; set; }
    public MappingProfile? MappingProfile { get; set; }

    // Which client's irentosoft login to use for the RPA push step (set when that step starts)
    public int? ClientAccountId { get; set; }
    public ClientAccount? ClientAccount { get; set; }

    // Captured once when the RPA run starts and reused on retries - not stored per-account
    // since a client's Excel data carries no location, and it can vary per upload.
    public string? RpaOwningLocation { get; set; }
    public string? RpaCurrentLocation { get; set; }

    // Result counters, populated after validation / commit
    public int TotalRecords { get; set; }
    public int ValidRecords { get; set; }
    public int WarningRecords { get; set; }
    public int ErrorRecords { get; set; }
    public int ImportedRecords { get; set; }
    public int FailedRecords { get; set; }

    public List<ImportRecord> Records { get; set; } = new();
    public List<AuditLog> AuditLogs { get; set; } = new();
}
