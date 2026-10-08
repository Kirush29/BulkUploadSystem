using System.Collections.Concurrent;
using System.Text.Json;
using BulkImportPlatform.Automation.PageObjects;
using BulkImportPlatform.Automation.Utils;
using BulkImportPlatform.Core.DTOs;
using BulkImportPlatform.Core.Entities;
using BulkImportPlatform.Core.Enums;
using BulkImportPlatform.Core.Interfaces;
using BulkImportPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace BulkImportPlatform.Automation;

/// <summary>
/// Pushes already-committed ImportRecords (Status == Imported, i.e. already a local
/// Vehicle/Customer row) into irentosoft via browser automation, using a specific client's
/// login. Runs as a distinct step after IImportService.CommitImportAsync - never folded into
/// it - so a slow/flaky external site can never jeopardize the fast, already-reliable local
/// commit. Mirrors CommitImportAsync's own record-iteration/status/audit-log pattern.
/// </summary>
public class RpaImportService : IRpaImportService
{
    private readonly BulkImportDbContext _db;
    private readonly IClientAccountCredentialProtector _protector;
    private readonly BrowserSessionFactory _browserFactory;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // One RPA run per ClientAccount at a time - irentosoft is someone else's live production
    // app with unknown rate-limiting/anti-bot posture; concurrent sessions against the same
    // login risk both triggering defenses and confusing concurrent-edit races.
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> AccountLocks = new();

    public RpaImportService(BulkImportDbContext db, IClientAccountCredentialProtector protector, BrowserSessionFactory browserFactory)
    {
        _db = db;
        _protector = protector;
        _browserFactory = browserFactory;
    }

    public async Task<RpaRunSummaryDto> RunAsync(int importJobId, string? owningLocation, string? currentLocation, string performedBy, RpaRunOptionsDto options)
    {
        var job = await LoadJobAsync(importJobId);
        if (job.ClientAccountId == null)
            throw new InvalidOperationException("This job has no client account assigned - re-upload through the wizard and pick one at the Upload step.");

        var account = await _db.ClientAccounts.FirstOrDefaultAsync(c => c.Id == job.ClientAccountId.Value)
            ?? throw new KeyNotFoundException($"Client account {job.ClientAccountId.Value} was not found.");
        if (!account.IsActive)
            throw new InvalidOperationException($"Client account '{account.Name}' is inactive.");

        job.RpaOwningLocation = owningLocation;
        job.RpaCurrentLocation = currentLocation;
        job.Status = ImportJobStatus.RpaInProgress;
        await _db.SaveChangesAsync();

        var eligible = job.Records
            .Where(r => r.Status == RecordStatus.Imported && (r.RpaStatus == RpaStatus.NotAttempted || r.RpaStatus == RpaStatus.Failed))
            .OrderBy(r => r.RowNumber)
            .ToList();

        await RunEligibleRecordsAsync(job, account, eligible, performedBy, options);

        return await BuildSummaryAsync(job.Id);
    }

    public async Task<RpaRunSummaryDto> RetryRecordsAsync(int importJobId, List<int> importRecordIds, string performedBy, RpaRunOptionsDto options)
    {
        var job = await LoadJobAsync(importJobId);
        if (job.ClientAccountId == null)
            throw new InvalidOperationException("This job has no RPA run yet - call RunAsync before retrying individual records.");

        var account = await _db.ClientAccounts.FirstOrDefaultAsync(c => c.Id == job.ClientAccountId.Value)
            ?? throw new KeyNotFoundException($"Client account {job.ClientAccountId.Value} was not found.");

        var eligible = job.Records.Where(r => importRecordIds.Contains(r.Id)).OrderBy(r => r.RowNumber).ToList();

        job.Status = ImportJobStatus.RpaInProgress;
        await _db.SaveChangesAsync();

        await RunEligibleRecordsAsync(job, account, eligible, performedBy, options);

        return await BuildSummaryAsync(job.Id);
    }

    public async Task<RpaRunSummaryDto> GetRpaStatusAsync(int importJobId) => await BuildSummaryAsync(importJobId);

    public async Task<RpaRunSummaryDto> UpdateRecordAndRetryAsync(int importJobId, int importRecordId, Dictionary<string, string?> fieldUpdates, string performedBy, RpaRunOptionsDto options)
    {
        var job = await LoadJobAsync(importJobId);
        if (job.ClientAccountId == null)
            throw new InvalidOperationException("This job has no RPA run yet - call RunAsync before editing individual records.");

        var record = job.Records.FirstOrDefault(r => r.Id == importRecordId)
            ?? throw new KeyNotFoundException($"Import record {importRecordId} was not found on job {importJobId}.");

        if (record.RpaStatus != RpaStatus.Failed)
            throw new InvalidOperationException("Only records with a failed RPA attempt can be edited.");

        var account = await _db.ClientAccounts.FirstOrDefaultAsync(c => c.Id == job.ClientAccountId.Value)
            ?? throw new KeyNotFoundException($"Client account {job.ClientAccountId.Value} was not found.");

        var mappedRow = JsonSerializer.Deserialize<Dictionary<string, string?>>(record.MappedDataJson, JsonOptions) ?? new();
        foreach (var (field, value) in fieldUpdates)
        {
            mappedRow[field] = value;
        }
        record.MappedDataJson = JsonSerializer.Serialize(mappedRow);
        await _db.SaveChangesAsync();

        job.Status = ImportJobStatus.RpaInProgress;
        await _db.SaveChangesAsync();

        await RunEligibleRecordsAsync(job, account, new List<ImportRecord> { record }, performedBy, options);

        return await BuildSummaryAsync(job.Id);
    }

    // ---------- Core run loop ----------

    private async Task RunEligibleRecordsAsync(ImportJob job, ClientAccount account, List<ImportRecord> eligible, string performedBy, RpaRunOptionsDto options)
    {
        var accountLock = AccountLocks.GetOrAdd(account.Id, _ => new SemaphoreSlim(1, 1));
        await accountLock.WaitAsync();
        try
        {
            if (eligible.Count == 0)
            {
                job.Status = ComputeRpaJobStatus(job);
                await _db.SaveChangesAsync();
                return;
            }

            var password = _protector.Unprotect(account.EncryptedPassword);
            await using var session = await _browserFactory.CreateAsync(headed: options.Headed);

            var loginPage = new IrentosoftLoginPage(session.Page);
            var loginResult = await RetryPolicy.RetryAsync(() => loginPage.LoginAsync(account.IrentosoftUsername, password));

            if (loginResult != LoginResult.Success)
            {
                foreach (var record in eligible)
                {
                    await RecordAttemptAsync(record, RpaStatus.Failed, "Login",
                        $"Login failed ({loginResult}) for client account '{account.Name}'.", performedBy, null);
                }
                job.Status = ComputeRpaJobStatus(job);
                await _db.SaveChangesAsync();
                await LogAsync(job.Id, "RPA_RUN_COMPLETED", performedBy,
                    $"0 succeeded, {eligible.Count} failed (login failure) out of {eligible.Count} eligible.");
                return;
            }

            if (job.ImportType == ImportType.Vehicle && options.CreateMissingMakesAndModels)
            {
                await EnsureMakesAndModelsAsync(session.Page, job.Id, eligible, performedBy);
            }

            int succeeded = 0, failed = 0;
            foreach (var record in eligible)
            {
                var mappedRow = JsonSerializer.Deserialize<Dictionary<string, string?>>(record.MappedDataJson, JsonOptions) ?? new();

                var ok = job.ImportType == ImportType.Vehicle
                    ? await ProcessVehicleAsync(session.Page, mappedRow, job.RpaOwningLocation, job.RpaCurrentLocation, record, performedBy, options)
                    : await ProcessCustomerAsync(session.Page, mappedRow, record, performedBy, options);

                if (ok) succeeded++; else failed++;
            }

            // Reflects ALL of the job's eligible records, not just the ones processed in this
            // call - a single-record retry/edit must not silently mark the whole job "completed"
            // while other rows from an earlier run are still sitting there Failed.
            job.Status = ComputeRpaJobStatus(job);
            await _db.SaveChangesAsync();
            await LogAsync(job.Id, "RPA_RUN_COMPLETED", performedBy,
                $"{succeeded} succeeded, {failed} failed out of {eligible.Count} eligible.");
        }
        finally
        {
            accountLock.Release();
        }
    }

    /// <summary>
    /// Adds every Make, and every Make+Model pair, used by <paramref name="records"/> that the
    /// account's irentosoft catalog doesn't already have - once per run, before any vehicle is
    /// entered, so the per-vehicle dropdowns then just find them.
    ///
    /// Duplicate safety: "already exists" is decided against the live Add-Asset dropdown lists
    /// (trimmed, case-insensitive), the batch's own values are de-duplicated the same way, and
    /// the per-account lock held by the caller rules out a concurrent run adding the same entry.
    /// Each create is re-read from the dropdown afterwards; a failure is logged and only
    /// affects the rows that need that make/model (they fail at the dropdown, as before).
    /// </summary>
    private async Task EnsureMakesAndModelsAsync(IPage page, int importJobId, List<ImportRecord> records, string performedBy)
    {
        var wanted = new Dictionary<string, (string Make, HashSet<string> Models)>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            var row = JsonSerializer.Deserialize<Dictionary<string, string?>>(record.MappedDataJson, JsonOptions) ?? new();
            var make = Get(row, "Make")?.Trim();
            if (string.IsNullOrWhiteSpace(make)) continue;

            if (!wanted.TryGetValue(make, out var entry))
            {
                entry = (make, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                wanted[make] = entry;
            }
            var model = Get(row, "Model")?.Trim();
            if (!string.IsNullOrWhiteSpace(model)) entry.Models.Add(model);
        }
        if (wanted.Count == 0) return;

        var vehiclePage = new VehicleCreatePage(page);
        var settingsPage = new AssetCatalogSettingsPage(page);
        var createdMakes = new List<string>();
        var createdModels = new List<string>();
        var problems = new List<string>();

        try
        {
            await vehiclePage.NavigateAsync();
            var existingMakes = ToLookupSet(await vehiclePage.ReadMakesAsync());

            foreach (var (make, _) in wanted.Values)
            {
                if (existingMakes.Contains(make)) continue;
                try
                {
                    await settingsPage.CreateMakeAsync(make);
                    createdMakes.Add(make);
                }
                catch (Exception ex)
                {
                    problems.Add($"Make '{make}': {ex.Message}");
                }
            }

            if (createdMakes.Count > 0)
            {
                await vehiclePage.NavigateAsync();
                existingMakes = ToLookupSet(await vehiclePage.ReadMakesAsync());
                foreach (var make in createdMakes.Where(m => !existingMakes.Contains(m)))
                    problems.Add($"Make '{make}': saved, but it does not appear in the Add-Asset Make list.");
            }

            foreach (var (make, models) in wanted.Values)
            {
                if (models.Count == 0 || !existingMakes.Contains(make)) continue;

                await vehiclePage.NavigateAsync();
                var existingModels = ToLookupSet(await vehiclePage.ReadModelsForMakeAsync(make));
                var missing = models.Where(m => !existingModels.Contains(m)).ToList();
                if (missing.Count == 0) continue;

                var createdForMake = new List<string>();
                foreach (var model in missing)
                {
                    try
                    {
                        await settingsPage.CreateModelAsync(make, model);
                        createdForMake.Add(model);
                    }
                    catch (Exception ex)
                    {
                        problems.Add($"Model '{make} {model}': {ex.Message}");
                    }
                }

                if (createdForMake.Count == 0) continue;
                await vehiclePage.NavigateAsync();
                existingModels = ToLookupSet(await vehiclePage.ReadModelsForMakeAsync(make));
                foreach (var model in createdForMake)
                {
                    if (existingModels.Contains(model)) createdModels.Add($"{make} {model}");
                    else problems.Add($"Model '{make} {model}': saved, but it does not appear in the Add-Asset Model list.");
                }
            }
        }
        catch (Exception ex)
        {
            // Never let the catalog step abort the push itself - rows whose make/model is
            // still missing will fail individually with the usual dropdown message.
            problems.Add($"Make/model sync stopped early: {ex.Message}");
        }

        var details = $"Makes added: {(createdMakes.Count > 0 ? string.Join(", ", createdMakes) : "none")}. " +
                      $"Models added: {(createdModels.Count > 0 ? string.Join(", ", createdModels) : "none")}.";
        if (problems.Count > 0) details += " Problems: " + string.Join(" | ", problems);
        await LogAsync(importJobId, "RPA_CATALOG_SYNC", performedBy, details);
    }

    private static HashSet<string> ToLookupSet(IEnumerable<string> values) =>
        new(values.Select(v => v.Trim()), StringComparer.OrdinalIgnoreCase);

    private async Task<bool> ProcessVehicleAsync(IPage page, Dictionary<string, string?> row, string? owningLocation, string? currentLocation, ImportRecord record, string performedBy, RpaRunOptionsDto options)
    {
        var vehiclePage = new VehicleCreatePage(page);
        string? failureStage = null;
        string? failureMessage = null;
        string? screenshotPath = null;
        var success = false;

        try
        {
            await vehiclePage.NavigateAsync();

            if (!string.IsNullOrWhiteSpace(owningLocation)) await vehiclePage.SetOwningLocationAsync(owningLocation);
            if (!string.IsNullOrWhiteSpace(currentLocation)) await vehiclePage.SetCurrentLocationAsync(currentLocation);

            await SetIfPresent(row, "VehicleNumber", vehiclePage.SetCodeAsync);
            await SetIfPresent(row, "VIN", vehiclePage.SetVinAsync);
            await SetIfPresent(row, "Make", vehiclePage.SetMakeAsync);
            await SetIfPresent(row, "Model", vehiclePage.SetModelAsync);
            await SetIfPresent(row, "VehicleType", vehiclePage.SetVehicleTypeAsync);
            await SetIfPresent(row, "LicenseNumber", vehiclePage.SetLicenseNumberAsync);
            await SetIfPresent(row, "Transmission", vehiclePage.SetTransmissionAsync);
            await SetIfPresent(row, "Year", vehiclePage.SetYearAsync);
            await SetIfPresent(row, "Color", vehiclePage.SetColorAsync);
            await SetIfPresent(row, "FuelType", vehiclePage.SetFuelTypeAsync);
            await SetIfPresent(row, "TankSize", vehiclePage.SetTankSizeAsync);
            await SetIfPresent(row, "Odometer", vehiclePage.SetOdometerAsync);
            await SetIfPresent(row, "SpotNumber", vehiclePage.SetSpotNumberAsync);

            var outcome = await vehiclePage.SaveAsync();
            if (outcome.Success) success = true;
            else { failureStage = "Save"; failureMessage = outcome.Message; }
        }
        catch (DropdownOptionNotFoundException ex)
        {
            // Screenshot immediately - DropdownSelector throws right after opening the dropdown
            // and finding no match, so the dropdown's real option list is still on screen here.
            if (options.CaptureScreenshotsOnFailure) screenshotPath = await CaptureScreenshotAsync(page, record.Id);
            failureStage = $"SelectDropdown:{ex.FieldName}";
            failureMessage = ex.Message;
        }
        catch (Exception ex)
        {
            if (options.CaptureScreenshotsOnFailure) screenshotPath = await CaptureScreenshotAsync(page, record.Id);
            failureStage = "Unknown";
            failureMessage = ex.Message;
        }

        if (success)
            await RecordAttemptAsync(record, RpaStatus.Succeeded, null, null, performedBy, null);
        else
            await RecordAttemptAsync(record, RpaStatus.Failed, failureStage, failureMessage, performedBy, screenshotPath);

        return success;
    }

    private async Task<bool> ProcessCustomerAsync(IPage page, Dictionary<string, string?> row, ImportRecord record, string performedBy, RpaRunOptionsDto options)
    {
        var customerName = Get(row, "CustomerName") ?? string.Empty;
        var split = SplitName(customerName);
        if (split == null)
        {
            await RecordAttemptAsync(record, RpaStatus.Failed, "NameSplit",
                $"Cannot split customer name '{customerName}' into first/last name for irentosoft - it must include both (e.g. 'John Smith').",
                performedBy, null);
            return false;
        }

        var customerPage = new CustomerCreatePage(page);
        string? failureStage = null;
        string? failureMessage = null;
        string? screenshotPath = null;
        var success = false;

        try
        {
            await customerPage.NavigateAsync();

            await customerPage.SetFirstNameAsync(split.Value.First);
            await customerPage.SetLastNameAsync(split.Value.Last);

            await SetIfPresent(row, "Email", customerPage.SetEmailAsync);
            await SetIfPresent(row, "Phone", customerPage.SetPhoneAsync);
            await SetIfPresent(row, "DriverLicenseNumber", customerPage.SetDriverLicenseNumberAsync);
            await SetIfPresent(row, "City", customerPage.SetCityAsync);
            await SetIfPresent(row, "Address", customerPage.SetStreetAsync);
            await SetIfPresent(row, "Country", customerPage.SetCountryAsync);
            await SetIfPresent(row, "State", customerPage.SetStateAsync);

            var dob = FormatDateForIrentosoft(Get(row, "DateOfBirth"));
            if (!string.IsNullOrWhiteSpace(dob)) await customerPage.SetDateOfBirthAsync(dob);

            var outcome = await customerPage.SaveAsync();
            if (outcome.Success) success = true;
            else { failureStage = "Save"; failureMessage = outcome.Message; }
        }
        catch (DropdownOptionNotFoundException ex)
        {
            if (options.CaptureScreenshotsOnFailure) screenshotPath = await CaptureScreenshotAsync(page, record.Id);
            failureStage = $"SelectDropdown:{ex.FieldName}";
            failureMessage = ex.Message;
        }
        catch (Exception ex)
        {
            if (options.CaptureScreenshotsOnFailure) screenshotPath = await CaptureScreenshotAsync(page, record.Id);
            failureStage = "Unknown";
            failureMessage = ex.Message;
        }

        if (success)
            await RecordAttemptAsync(record, RpaStatus.Succeeded, null, null, performedBy, null);
        else
            await RecordAttemptAsync(record, RpaStatus.Failed, failureStage, failureMessage, performedBy, screenshotPath);

        return success;
    }

    // ---------- Helpers ----------

    private async Task<ImportJob> LoadJobAsync(int importJobId) =>
        await _db.ImportJobs
            .Include(j => j.Records).ThenInclude(r => r.RpaAttempts)
            .FirstOrDefaultAsync(j => j.Id == importJobId)
        ?? throw new KeyNotFoundException($"Import job {importJobId} was not found.");

    private async Task<RpaRunSummaryDto> BuildSummaryAsync(int importJobId)
    {
        var job = await LoadJobAsync(importJobId);
        var eligible = job.Records.Where(r => r.Status == RecordStatus.Imported).OrderBy(r => r.RowNumber).ToList();

        return new RpaRunSummaryDto
        {
            ImportJobId = job.Id,
            Status = job.Status,
            TotalEligible = eligible.Count,
            Succeeded = eligible.Count(r => r.RpaStatus == RpaStatus.Succeeded),
            Failed = eligible.Count(r => r.RpaStatus == RpaStatus.Failed),
            Skipped = eligible.Count(r => r.RpaStatus == RpaStatus.Skipped),
            Records = eligible.Select(r => new RpaRecordResultDto
            {
                ImportRecordId = r.Id,
                RowNumber = r.RowNumber,
                RpaStatus = r.RpaStatus,
                RpaMessage = r.RpaMessage,
                AttemptCount = r.RpaAttempts.Count,
                LastScreenshotPath = Path.GetFileName(
                    r.RpaAttempts.OrderByDescending(a => a.AttemptNumber).FirstOrDefault(a => a.ScreenshotPath != null)?.ScreenshotPath),
                MappedData = JsonSerializer.Deserialize<Dictionary<string, string?>>(r.MappedDataJson, JsonOptions) ?? new()
            }).ToList()
        };
    }

    private async Task RecordAttemptAsync(ImportRecord record, RpaStatus outcome, string? failureStage, string? message, string performedBy, string? screenshotPath)
    {
        _db.RpaAttempts.Add(new RpaAttempt
        {
            ImportRecordId = record.Id,
            AttemptNumber = record.RpaAttempts.Count + 1,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Outcome = outcome,
            FailureStage = failureStage,
            ErrorMessage = message,
            ScreenshotPath = screenshotPath,
            PerformedBy = performedBy
        });
        record.RpaStatus = outcome;
        record.RpaMessage = message;
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Best-effort full-page screenshot taken at the moment of failure - e.g. a dropdown that
    /// just threw DropdownOptionNotFoundException is still open on screen, so this captures the
    /// real available options directly instead of requiring someone to reproduce it manually.
    /// Never lets a screenshot failure mask the real error being recorded.
    /// </summary>
    private static async Task<string?> CaptureScreenshotAsync(IPage page, int importRecordId)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "RpaEvidence", "Screenshots");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"record-{importRecordId}-{DateTime.UtcNow:yyyyMMddHHmmssfff}.png");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
            return path;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Overall RPA status across every eligible record on the job, not just whichever subset a particular run/retry call touched.</summary>
    private static ImportJobStatus ComputeRpaJobStatus(ImportJob job)
    {
        var eligible = job.Records.Where(r => r.Status == RecordStatus.Imported).ToList();
        if (eligible.Count == 0) return ImportJobStatus.RpaCompleted;
        return eligible.Any(r => r.RpaStatus == RpaStatus.Failed) ? ImportJobStatus.RpaCompletedWithErrors : ImportJobStatus.RpaCompleted;
    }

    private async Task LogAsync(int importJobId, string action, string performedBy, string details)
    {
        _db.AuditLogs.Add(new AuditLog { ImportJobId = importJobId, Action = action, PerformedBy = performedBy, Details = details });
        await _db.SaveChangesAsync();
    }

    private static async Task SetIfPresent(Dictionary<string, string?> row, string field, Func<string, Task> setter)
    {
        var value = Get(row, field);
        if (!string.IsNullOrWhiteSpace(value)) await setter(value);
    }

    private static string? Get(Dictionary<string, string?> row, string field) =>
        row.TryGetValue(field, out var value) ? value : null;

    /// <summary>Last word = last name, everything before it = first name (confirmed default for this client base).</summary>
    private static (string First, string Last)? SplitName(string customerName)
    {
        var trimmed = customerName.Trim();
        var lastSpace = trimmed.LastIndexOf(' ');
        if (lastSpace <= 0) return null;

        var first = trimmed[..lastSpace].Trim();
        var last = trimmed[(lastSpace + 1)..].Trim();
        return string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last) ? null : (first, last);
    }

    private static string? FormatDateForIrentosoft(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? null : DateTime.TryParse(raw, out var dt) ? dt.ToString("MM/dd/yyyy") : raw;
}
