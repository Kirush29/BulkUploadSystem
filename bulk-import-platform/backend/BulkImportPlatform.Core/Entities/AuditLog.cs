namespace BulkImportPlatform.Core.Entities;

/// <summary>
/// Records every meaningful action taken against an import job
/// ("AuditLogs" table from the proposal) - upload, mapping saved,
/// validation run, import committed, etc.
/// </summary>
public class AuditLog
{
    public int Id { get; set; }

    public int? ImportJobId { get; set; }
    public ImportJob? ImportJob { get; set; }

    public string Action { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Details { get; set; }
}
