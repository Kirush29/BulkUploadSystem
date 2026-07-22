using System.Text.Json;
using BulkImportPlatform.Core;
using BulkImportPlatform.Core.DTOs;
using BulkImportPlatform.Core.Entities;
using BulkImportPlatform.Core.Enums;
using BulkImportPlatform.Core.Interfaces;
using BulkImportPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BulkImportPlatform.Infrastructure.Services;

/// <summary>
/// Orchestrates the 9-step import workflow from section 8 of the proposal:
/// Upload -> Parse -> Field Mapping -> Validation -> Default Values -> Preview/Review ->
/// Bulk Import Execution -> Result Generation -> Audit Recording.
/// </summary>
public class ImportService : IImportService
{
    private readonly BulkImportDbContext _db;
    private readonly IExcelParserService _parser;
    private readonly IMappingEngine _mappingEngine;
    private readonly IDefaultValueEngine _defaultValueEngine;
    private readonly IValidationEngine _validationEngine;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ImportService(
        BulkImportDbContext db,
        IExcelParserService parser,
        IMappingEngine mappingEngine,
        IDefaultValueEngine defaultValueEngine,
        IValidationEngine validationEngine)
    {
        _db = db;
        _parser = parser;
        _mappingEngine = mappingEngine;
        _defaultValueEngine = defaultValueEngine;
        _validationEngine = validationEngine;
    }

    // ---------- Step 1-2: Upload + Parse ----------

    public async Task<UploadResultDto> UploadAsync(ImportType type, int clientAccountId, Stream fileStream, string fileName, string uploadedBy)
    {
        var clientAccount = await _db.ClientAccounts.FirstOrDefaultAsync(c => c.Id == clientAccountId)
            ?? throw new KeyNotFoundException($"Client account {clientAccountId} was not found.");
        if (!clientAccount.IsActive)
            throw new InvalidOperationException($"Client account '{clientAccount.Name}' is inactive.");

        ParsedSheet parsed;
        try
        {
            parsed = _parser.Parse(fileStream, fileName);
        }
        catch (InvalidDataException)
        {
            // Still record the failed attempt for auditability, per section 5's critical-error rules
            // ("File format is invalid", "Uploaded file is corrupted").
            var failedJob = new ImportJob
            {
                ImportType = type,
                ClientAccountId = clientAccountId,
                FileName = fileName,
                UploadedBy = uploadedBy,
                Status = ImportJobStatus.Failed
            };
            _db.ImportJobs.Add(failedJob);
            await _db.SaveChangesAsync();
            await LogAsync(failedJob.Id, "UPLOAD_FAILED", uploadedBy, "File format invalid or corrupted.");
            throw;
        }

        var job = new ImportJob
        {
            ImportType = type,
            ClientAccountId = clientAccountId,
            FileName = fileName,
            UploadedBy = uploadedBy,
            Status = ImportJobStatus.Uploaded,
            DetectedHeadersJson = JsonSerializer.Serialize(parsed.Headers),
            TotalRecords = parsed.Rows.Count
        };
        _db.ImportJobs.Add(job);
        await _db.SaveChangesAsync();

        // Persist raw rows immediately so mapping/validation can be re-run without re-uploading.
        var rowNumber = 1;
        foreach (var row in parsed.Rows)
        {
            _db.ImportRecords.Add(new ImportRecord
            {
                ImportJobId = job.Id,
                RowNumber = ++rowNumber, // row 1 is the header row
                RawDataJson = JsonSerializer.Serialize(row),
                Status = RecordStatus.Valid
            });
        }
        await _db.SaveChangesAsync();

        await LogAsync(job.Id, "FILE_UPLOADED", uploadedBy, $"Uploaded '{fileName}' with {parsed.Rows.Count} data rows.");

        var suggestedMapping = _mappingEngine.SuggestMapping(parsed.Headers, type);

        return new UploadResultDto
        {
            ImportJobId = job.Id,
            ClientAccountId = clientAccountId,
            ClientAccountName = clientAccount.Name,
            FileName = fileName,
            DetectedHeaders = parsed.Headers,
            SampleRows = parsed.Rows.Take(10).ToList(),
            TotalRows = parsed.Rows.Count,
            SuggestedMapping = suggestedMapping,
            SystemFields = FieldDefinitions.For(type)
                .Select(f => new FieldDefinitionDto { SystemField = f.SystemField, IsMandatory = f.IsMandatory, DataType = f.DataType })
                .ToList()
        };
    }

    // ---------- Step 3-6: Mapping, Validation, Defaults, Preview ----------

    public async Task<ValidationSummaryDto> SaveMappingAndValidateAsync(int importJobId, SaveMappingRequestDto request, string performedBy)
    {
        var job = await _db.ImportJobs
            .Include(j => j.Records).ThenInclude(r => r.ValidationResults)
            .FirstOrDefaultAsync(j => j.Id == importJobId)
            ?? throw new KeyNotFoundException($"Import job {importJobId} was not found.");

        MappingProfile? profile = null;
        if (request.SaveAsReusableProfile)
        {
            profile = new MappingProfile
            {
                Name = string.IsNullOrWhiteSpace(request.ProfileName) ? $"{job.ImportType} mapping {DateTime.UtcNow:yyyy-MM-dd HH:mm}" : request.ProfileName!,
                ImportType = job.ImportType,
                ColumnMappingJson = JsonSerializer.Serialize(request.ColumnMapping),
                DefaultValuesJson = JsonSerializer.Serialize(request.DefaultValues),
                CreatedBy = performedBy
            };
            _db.MappingProfiles.Add(profile);
            await _db.SaveChangesAsync();
            job.MappingProfileId = profile.Id;
        }

        job.Status = ImportJobStatus.Mapped;
        await _db.SaveChangesAsync();
        await LogAsync(job.Id, "MAPPING_SAVED", performedBy, $"Column mapping applied ({request.ColumnMapping.Count} columns mapped).");

        // ---------- Apply mapping + defaults + validation across all rows ----------

        var rawRows = job.Records
            .OrderBy(r => r.RowNumber)
            .Select(r => JsonSerializer.Deserialize<Dictionary<string, string?>>(r.RawDataJson, JsonOptions) ?? new())
            .ToList();

        var mappedRows = _mappingEngine.ApplyMapping(rawRows, request.ColumnMapping, job.ImportType);

        // Scoped to this job's client account - VehicleNumber is unique per client, not
        // globally, so two different clients may legitimately share the same vehicle number.
        var existingVehicleNumbers = job.ImportType == ImportType.Vehicle
            ? (await _db.Vehicles.Where(v => v.ClientAccountId == job.ClientAccountId).Select(v => v.VehicleNumber).ToListAsync())
                .Select(v => v.Trim().ToUpperInvariant()).ToHashSet()
            : new HashSet<string>();

        // Count in-file occurrences of each vehicle number up-front so duplicate detection works
        // regardless of row order.
        var vehicleNumberCounts = new Dictionary<string, int>();
        if (job.ImportType == ImportType.Vehicle)
        {
            foreach (var row in mappedRows)
            {
                if (row.TryGetValue("VehicleNumber", out var vn) && !string.IsNullOrWhiteSpace(vn))
                {
                    var key = vn.Trim().ToUpperInvariant();
                    vehicleNumberCounts[key] = vehicleNumberCounts.GetValueOrDefault(key) + 1;
                }
            }
        }

        var records = job.Records.OrderBy(r => r.RowNumber).ToList();
        int validCount = 0, warningCount = 0, errorCount = 0;

        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            var mappedRow = mappedRows[i];

            var defaultsApplied = _defaultValueEngine.ApplyDefaults(mappedRow, request.DefaultValues, job.ImportType);

            var validationResults = _validationEngine.Validate(mappedRow, job.ImportType, vehicleNumberCounts, existingVehicleNumbers);

            record.MappedDataJson = JsonSerializer.Serialize(mappedRow);
            record.DefaultsAppliedJson = JsonSerializer.Serialize(defaultsApplied);

            // Replace any previously stored validation results for this record.
            _db.ValidationResults.RemoveRange(record.ValidationResults);
            record.ValidationResults = validationResults.Select(v => new ValidationResult
            {
                ImportRecordId = record.Id,
                Severity = v.Severity,
                FieldName = v.FieldName,
                RuleCode = v.RuleCode,
                Message = v.Message
            }).ToList();

            var hasError = validationResults.Any(v => v.Severity == ValidationSeverity.Error);
            var hasWarning = validationResults.Any(v => v.Severity == ValidationSeverity.Warning);

            record.Status = hasError ? RecordStatus.Error : hasWarning ? RecordStatus.Warning : RecordStatus.Valid;

            if (hasError) errorCount++;
            else if (hasWarning) warningCount++;
            else validCount++;
        }

        job.ValidRecords = validCount;
        job.WarningRecords = warningCount;
        job.ErrorRecords = errorCount;
        job.Status = ImportJobStatus.Validated;

        await _db.SaveChangesAsync();
        await LogAsync(job.Id, "VALIDATION_COMPLETED", performedBy,
            $"Validated {records.Count} records: {validCount} valid, {warningCount} with warnings, {errorCount} with errors.");

        return await GetValidationSummaryAsync(job.Id);
    }

    public async Task<ValidationSummaryDto> GetValidationSummaryAsync(int importJobId)
    {
        var job = await _db.ImportJobs
            .Include(j => j.Records).ThenInclude(r => r.ValidationResults)
            .FirstOrDefaultAsync(j => j.Id == importJobId)
            ?? throw new KeyNotFoundException($"Import job {importJobId} was not found.");

        return new ValidationSummaryDto
        {
            ImportJobId = job.Id,
            Status = job.Status,
            TotalRecords = job.Records.Count,
            ValidRecords = job.ValidRecords,
            WarningRecords = job.WarningRecords,
            ErrorRecords = job.ErrorRecords,
            Records = job.Records.OrderBy(r => r.RowNumber).Select(r => new ImportRecordDto
            {
                Id = r.Id,
                RowNumber = r.RowNumber,
                MappedData = JsonSerializer.Deserialize<Dictionary<string, string?>>(r.MappedDataJson, JsonOptions) ?? new(),
                DefaultsApplied = JsonSerializer.Deserialize<List<string>>(r.DefaultsAppliedJson, JsonOptions) ?? new(),
                Status = r.Status,
                ValidationResults = r.ValidationResults.Select(v => new ValidationResultDto
                {
                    Severity = v.Severity,
                    FieldName = v.FieldName,
                    RuleCode = v.RuleCode,
                    Message = v.Message
                }).ToList()
            }).ToList()
        };
    }

    // ---------- Step 7-9: Bulk Import, Result Generation, Audit Recording ----------

    public async Task<ImportSummaryReportDto> CommitImportAsync(int importJobId, string performedBy)
    {
        var job = await _db.ImportJobs
            .Include(j => j.Records).ThenInclude(r => r.ValidationResults)
            .FirstOrDefaultAsync(j => j.Id == importJobId)
            ?? throw new KeyNotFoundException($"Import job {importJobId} was not found.");

        if (job.Status != ImportJobStatus.Validated)
        {
            throw new InvalidOperationException("The import job must be validated before it can be committed.");
        }

        job.Status = ImportJobStatus.Importing;
        await _db.SaveChangesAsync();

        var importable = job.Records
            .Where(r => r.Status == RecordStatus.Valid || r.Status == RecordStatus.Warning)
            .OrderBy(r => r.RowNumber)
            .ToList();

        int imported = 0, failed = 0;

        foreach (var record in importable)
        {
            var mappedRow = JsonSerializer.Deserialize<Dictionary<string, string?>>(record.MappedDataJson, JsonOptions) ?? new();
            try
            {
                if (job.ImportType == ImportType.Vehicle)
                {
                    var vehicle = new Vehicle
                    {
                        VehicleNumber = Get(mappedRow, "VehicleNumber")!,
                        VIN = Get(mappedRow, "VIN"),
                        Make = Get(mappedRow, "Make"),
                        Model = Get(mappedRow, "Model"),
                        Year = TryInt(Get(mappedRow, "Year")),
                        Color = Get(mappedRow, "Color"),
                        FuelType = Get(mappedRow, "FuelType"),
                        TankSize = TryDecimal(Get(mappedRow, "TankSize")),
                        Odometer = TryDecimal(Get(mappedRow, "Odometer")),
                        LicenseNumber = Get(mappedRow, "LicenseNumber"),
                        SpotNumber = Get(mappedRow, "SpotNumber"),
                        Transmission = Get(mappedRow, "Transmission"),
                        VehicleType = Get(mappedRow, "VehicleType"),
                        CreatedBy = performedBy,
                        ImportJobId = job.Id,
                        ImportRecordId = record.Id,
                        ClientAccountId = job.ClientAccountId
                    };
                    _db.Vehicles.Add(vehicle);
                    await _db.SaveChangesAsync();
                    record.CreatedEntityId = vehicle.Id;
                }
                else
                {
                    var customer = new Customer
                    {
                        CustomerName = Get(mappedRow, "CustomerName")!,
                        Email = Get(mappedRow, "Email"),
                        Phone = Get(mappedRow, "Phone"),
                        Address = Get(mappedRow, "Address"),
                        City = Get(mappedRow, "City"),
                        State = Get(mappedRow, "State"),
                        Country = Get(mappedRow, "Country"),
                        DateOfBirth = TryDate(Get(mappedRow, "DateOfBirth")),
                        DriverLicenseNumber = Get(mappedRow, "DriverLicenseNumber"),
                        Status = Get(mappedRow, "Status"),
                        CreatedBy = performedBy,
                        ImportJobId = job.Id,
                        ImportRecordId = record.Id,
                        ClientAccountId = job.ClientAccountId
                    };
                    _db.Customers.Add(customer);
                    await _db.SaveChangesAsync();
                    record.CreatedEntityId = customer.Id;
                }

                record.Status = RecordStatus.Imported;
                imported++;
            }
            catch (Exception ex)
            {
                record.Status = RecordStatus.Skipped;
                record.ValidationResults.Add(new ValidationResult
                {
                    ImportRecordId = record.Id,
                    Severity = ValidationSeverity.Error,
                    FieldName = "General",
                    RuleCode = "COMMIT_FAILED",
                    Message = $"Failed to import row: {ex.Message}"
                });
                failed++;
            }
        }

        // Rows that had critical errors were never eligible for import.
        var rejected = job.Records.Count(r => r.Status == RecordStatus.Error);
        failed += rejected;

        job.ImportedRecords = imported;
        job.FailedRecords = failed;
        job.CompletedAt = DateTime.UtcNow;
        job.Status = failed > 0 ? ImportJobStatus.CompletedWithErrors : ImportJobStatus.Completed;

        await _db.SaveChangesAsync();
        await LogAsync(job.Id, "IMPORT_COMMITTED", performedBy,
            $"Committed import: {imported} imported, {failed} failed/rejected out of {job.Records.Count} total records.");

        return new ImportSummaryReportDto
        {
            ImportJobId = job.Id,
            Status = job.Status,
            TotalRecords = job.Records.Count,
            ImportedRecords = imported,
            FailedRecords = failed,
            WarningRecords = job.WarningRecords,
            CompletedAt = job.CompletedAt
        };
    }

    // ---------- Reporting: Upload History, Dashboard, Audit Log ----------

    public async Task<List<UploadHistoryItemDto>> GetUploadHistoryAsync(ImportType? type, int take)
    {
        var query = _db.ImportJobs.AsQueryable();
        if (type.HasValue) query = query.Where(j => j.ImportType == type.Value);

        return await query
            .OrderByDescending(j => j.UploadedAt)
            .Take(take)
            .Select(j => new UploadHistoryItemDto
            {
                ImportJobId = j.Id,
                UploadedAt = j.UploadedAt,
                UploadedBy = j.UploadedBy,
                FileName = j.FileName,
                ClientAccountName = j.ClientAccount != null ? j.ClientAccount.Name : null,
                ImportType = j.ImportType,
                Status = j.Status,
                TotalRecords = j.TotalRecords,
                ImportedRecords = j.ImportedRecords,
                FailedRecords = j.FailedRecords,
                WarningRecords = j.WarningRecords
            })
            .ToListAsync();
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync()
    {
        var today = DateTime.UtcNow.Date;

        var totalJobs = await _db.ImportJobs.CountAsync();
        var totalVehicles = await _db.Vehicles.CountAsync();
        var totalCustomers = await _db.Customers.CountAsync();
        var inProgress = await _db.ImportJobs.CountAsync(j =>
            j.Status == ImportJobStatus.Uploaded || j.Status == ImportJobStatus.Mapped ||
            j.Status == ImportJobStatus.Validated || j.Status == ImportJobStatus.Importing);
        var completedToday = await _db.ImportJobs.CountAsync(j =>
            j.CompletedAt != null && j.CompletedAt.Value.Date == today);

        var recent = await GetUploadHistoryAsync(null, 10);

        return new DashboardSummaryDto
        {
            TotalImportJobs = totalJobs,
            TotalVehiclesImported = totalVehicles,
            TotalCustomersImported = totalCustomers,
            JobsInProgress = inProgress,
            JobsCompletedToday = completedToday,
            RecentJobs = recent
        };
    }

    public async Task<List<AuditLogDto>> GetAuditLogsAsync(int? importJobId, int take)
    {
        var query = _db.AuditLogs.AsQueryable();
        if (importJobId.HasValue) query = query.Where(a => a.ImportJobId == importJobId.Value);

        return await query
            .OrderByDescending(a => a.Timestamp)
            .Take(take)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                ImportJobId = a.ImportJobId,
                Action = a.Action,
                PerformedBy = a.PerformedBy,
                Timestamp = a.Timestamp,
                Details = a.Details
            })
            .ToListAsync();
    }

    // ---------- Helpers ----------

    private async Task LogAsync(int importJobId, string action, string performedBy, string details)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            ImportJobId = importJobId,
            Action = action,
            PerformedBy = performedBy,
            Details = details
        });
        await _db.SaveChangesAsync();
    }

    private static string? Get(Dictionary<string, string?> row, string field) =>
        row.TryGetValue(field, out var value) ? value : null;

    private static int? TryInt(string? value) => int.TryParse(value, out var result) ? result : null;

    private static decimal? TryDecimal(string? value) => decimal.TryParse(value, out var result) ? result : null;

    private static DateTime? TryDate(string? value) => DateTime.TryParse(value, out var result) ? result : null;
}
