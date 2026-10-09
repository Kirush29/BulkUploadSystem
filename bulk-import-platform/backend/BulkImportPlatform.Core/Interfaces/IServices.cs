using BulkImportPlatform.Core.DTOs;
using BulkImportPlatform.Core.Entities;
using BulkImportPlatform.Core.Enums;

namespace BulkImportPlatform.Core.Interfaces;

/// <summary>
/// Parses an uploaded spreadsheet into headers + rows. Implemented with ClosedXML
/// (File Processing Layer per the proposal's system architecture).
/// </summary>
public interface IExcelParserService
{
    ParsedSheet Parse(Stream fileStream, string fileName);

    /// <summary>Generates a downloadable Excel template for the given import type.</summary>
    byte[] GenerateTemplate(ImportType type);
}

public class ParsedSheet
{
    public List<string> Headers { get; set; } = new();
    public List<Dictionary<string, string?>> Rows { get; set; } = new();
}

/// <summary>
/// Applies a column-mapping profile to raw parsed rows, converting source column
/// names into system field names (Mapping Engine per the proposal's Business Layer).
/// </summary>
public interface IMappingEngine
{
    Dictionary<string, string> SuggestMapping(List<string> headers, ImportType type);

    List<Dictionary<string, string?>> ApplyMapping(
        List<Dictionary<string, string?>> rawRows,
        Dictionary<string, string> columnMapping,
        ImportType type);
}

/// <summary>
/// Assigns default values to missing fields (Default Value Processing, section 7).
/// Returns, per row, the list of system field names that received a default.
/// </summary>
public interface IDefaultValueEngine
{
    List<string> ApplyDefaults(
        Dictionary<string, string?> mappedRow,
        Dictionary<string, string> configuredDefaults,
        ImportType type);
}

/// <summary>
/// Runs critical-error and warning validation rules (section 5) against mapped rows.
/// </summary>
public interface IValidationEngine
{
    List<ValidationResultDto> Validate(
        Dictionary<string, string?> mappedRow,
        ImportType type,
        Dictionary<string, int> vehicleNumberCountsInFile,
        HashSet<string> existingVehicleNumbers,
        Dictionary<string, int> unpushedVehicleNumbers);

    /// <param name="reservationNumberCountsInFile">Occurrences of each (upper-cased) ReservationNumber in the file.</param>
    /// <param name="pushedReservationNumbers">ReservationNumber -> job id, for numbers this client account already has in irentosoft via an earlier import.</param>
    List<ValidationResultDto> ValidateReservation(
        Dictionary<string, string?> mappedRow,
        Dictionary<string, int> reservationNumberCountsInFile,
        Dictionary<string, int> pushedReservationNumbers);
}

/// <summary>
/// Orchestrates the full 9-step import workflow described in section 8 of the proposal.
/// </summary>
public interface IImportService
{
    /// <summary>
    /// clientAccountId must be picked up front, at upload time - not deferred to the RPA push
    /// step - because duplicate/uniqueness checking during validation is scoped per client
    /// account and needs to know which account's existing data to check against.
    /// </summary>
    Task<UploadResultDto> UploadAsync(ImportType type, int clientAccountId, Stream fileStream, string fileName, string uploadedBy);

    Task<ValidationSummaryDto> SaveMappingAndValidateAsync(int importJobId, SaveMappingRequestDto request, string performedBy);

    Task<ImportSummaryReportDto> CommitImportAsync(int importJobId, string performedBy);

    Task<ValidationSummaryDto> GetValidationSummaryAsync(int importJobId);

    Task<List<UploadHistoryItemDto>> GetUploadHistoryAsync(ImportType? type, int take);

    Task<DashboardSummaryDto> GetDashboardSummaryAsync();

    Task<List<AuditLogDto>> GetAuditLogsAsync(int? importJobId, int take);
}

/// <summary>
/// Encrypts/decrypts client irentosoft passwords at rest, backed by ASP.NET Core's
/// Data Protection API. Never store or log a plaintext password outside of this.
/// </summary>
public interface IClientAccountCredentialProtector
{
    string Protect(string plaintextPassword);
    string Unprotect(string ciphertext);
}

/// <summary>
/// Manages the per-client irentosoft login records used by the RPA push step.
/// </summary>
public interface IClientAccountService
{
    Task<List<ClientAccountDto>> ListAsync();
    Task<ClientAccountDto> GetAsync(int id);
    Task<ClientAccountDto> CreateAsync(CreateClientAccountRequestDto request, string performedBy);
    Task<ClientAccountDto> UpdateAsync(int id, UpdateClientAccountRequestDto request, string performedBy);
    Task ResetPasswordAsync(int id, ResetClientAccountPasswordRequestDto request, string performedBy);
}

/// <summary>
/// Drives irentosoft's own web forms to enter Vehicle/Customer records for a specific client
/// login, using data already committed locally by IImportService. Reservations instead go
/// through irentosoft's internal booking API (authenticated by the same browser login), which
/// is many times faster than form filling.
/// Runs as a distinct step after CommitImportAsync - never folded into it - since browser
/// automation against a separate live production system is slow and can fail independently
/// of the fast, local, already-reliable DB commit.
/// </summary>
public interface IRpaImportService
{
    /// <summary>
    /// Uses the ClientAccount already assigned to the job at upload time (see
    /// IImportService.UploadAsync) - there is no separate account choice here, since the
    /// job's local duplicate-checking was already scoped against that specific account.
    /// </summary>
    Task<RpaRunSummaryDto> RunAsync(int importJobId, string? owningLocation, string? currentLocation, string performedBy, RpaRunOptionsDto options);

    Task<RpaRunSummaryDto> RetryRecordsAsync(int importJobId, List<int> importRecordIds, string performedBy, RpaRunOptionsDto options);

    Task<RpaRunSummaryDto> GetRpaStatusAsync(int importJobId);

    /// <summary>
    /// Edits the already-mapped field data for one record (e.g. correcting a VehicleType/Model
    /// value that didn't match irentosoft's configured options) without touching the local
    /// Vehicle/Customer row already committed by IImportService, and without re-running
    /// per-account VehicleNumber uniqueness checks - this only fixes fields relevant to the RPA
    /// push, it does not re-validate or re-commit anything locally. Only allowed on records
    /// whose RpaStatus is Failed, so a mid-flight or never-attempted record can't be silently
    /// altered out from under the pipeline.
    /// </summary>
    Task<RpaRunSummaryDto> UpdateRecordAndRetryAsync(int importJobId, int importRecordId, Dictionary<string, string?> fieldUpdates, string performedBy, RpaRunOptionsDto options);
}
