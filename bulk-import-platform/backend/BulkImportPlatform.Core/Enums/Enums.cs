namespace BulkImportPlatform.Core.Enums;

public enum ImportType
{
    Vehicle = 1,
    Customer = 2,
    Reservation = 3
}

public enum ImportJobStatus
{
    Uploaded = 1,
    Mapped = 2,
    Validated = 3,
    Importing = 4,
    Completed = 5,
    CompletedWithErrors = 6,
    Failed = 7,
    RpaInProgress = 8,
    RpaCompleted = 9,
    RpaCompletedWithErrors = 10
}

public enum RecordStatus
{
    Valid = 1,
    Warning = 2,
    Error = 3,
    Imported = 4,
    Skipped = 5
}

public enum ValidationSeverity
{
    Warning = 1,
    Error = 2
}

public enum RpaStatus
{
    NotAttempted = 1,
    InProgress = 2,
    Succeeded = 3,
    Failed = 4,
    Skipped = 5
}
