using BulkImportPlatform.Core.Enums;

namespace BulkImportPlatform.Core.Entities;

/// <summary>
/// One attempt to enter a single ImportRecord into irentosoft via browser automation.
/// Kept as full history (rather than overwriting a single status/message on ImportRecord)
/// so staff can see why an earlier attempt failed even after a later retry succeeds -
/// mirrors the existing AuditLog/ValidationResult one-parent-many-detail-rows pattern.
/// </summary>
public class RpaAttempt
{
    public int Id { get; set; }

    public int ImportRecordId { get; set; }
    public ImportRecord? ImportRecord { get; set; }

    public int AttemptNumber { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public RpaStatus Outcome { get; set; }

    // e.g. "Login", "FillBasicInfo", "SelectDropdown:makeId", "Save", "DuplicateModal"
    public string? FailureStage { get; set; }
    public string? ErrorMessage { get; set; }

    // Populated only in headed/debug mode (RpaRunOptions.CaptureScreenshotsOnFailure)
    public string? ScreenshotPath { get; set; }

    public string PerformedBy { get; set; } = string.Empty;
}
