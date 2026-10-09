using BulkImportPlatform.Core.DTOs;
using BulkImportPlatform.Core.Enums;
using BulkImportPlatform.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BulkImportPlatform.Api.Controllers;

/// <summary>
/// Drives the Vehicle and Customer bulk import workflow (upload -> map -> validate ->
/// preview -> commit) described in sections 5 and 8 of the proposal.
/// Route segment {type} must be "vehicle" or "customer".
/// </summary>
[ApiController]
[Route("api/imports/{type}")]
public class ImportsController : ControllerBase
{
    private readonly IImportService _importService;

    public ImportsController(IImportService importService)
    {
        _importService = importService;
    }

    /// <summary>Current user placeholder - replace with real authentication (see NFR: Security).</summary>
    private string CurrentUser => Request.Headers.TryGetValue("X-User", out var user) && !string.IsNullOrWhiteSpace(user)
        ? user.ToString()
        : "demo-user";

    private static ImportType ParseType(string type) => type.Trim().ToLowerInvariant() switch
    {
        "vehicle" or "vehicles" => ImportType.Vehicle,
        "customer" or "customers" => ImportType.Customer,
        "reservation" or "reservations" => ImportType.Reservation,
        _ => throw new ArgumentException($"Unknown import type '{type}'. Use 'vehicle', 'customer' or 'reservation'.")
    };

    // Step 1-2: Upload Excel File / Parse Spreadsheet Data
    // clientAccountId is required here (not deferred to the RPA push step) since duplicate/
    // uniqueness checking during validation is scoped per client account.
    [HttpPost("upload")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<ActionResult<UploadResultDto>> Upload(string type, IFormFile file, [FromForm] int clientAccountId)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file was uploaded." });
        }

        try
        {
            var importType = ParseType(type);
            await using var stream = file.OpenReadStream();
            var result = await _importService.UploadAsync(importType, clientAccountId, stream, file.FileName, CurrentUser);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidDataException ex)
        {
            // Critical error per section 5: invalid format / corrupted file
            return UnprocessableEntity(new { message = ex.Message });
        }
    }

    // Step 3-6: Field Mapping, Validation Processing, Default Value Assignment, Preview/Review
    [HttpPost("{importJobId:int}/validate")]
    public async Task<ActionResult<ValidationSummaryDto>> ValidateAndPreview(string type, int importJobId, [FromBody] SaveMappingRequestDto request)
    {
        try
        {
            var result = await _importService.SaveMappingAndValidateAsync(importJobId, request, CurrentUser);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("{importJobId:int}")]
    public async Task<ActionResult<ValidationSummaryDto>> GetPreview(string type, int importJobId)
    {
        try
        {
            var result = await _importService.GetValidationSummaryAsync(importJobId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // Step 7-9: Bulk Import Execution, Result Generation, Audit Recording
    [HttpPost("{importJobId:int}/commit")]
    public async Task<ActionResult<ImportSummaryReportDto>> Commit(string type, int importJobId)
    {
        try
        {
            var result = await _importService.CommitImportAsync(importJobId, CurrentUser);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // Upload History Report
    [HttpGet("history")]
    public async Task<ActionResult<List<UploadHistoryItemDto>>> History(string type, [FromQuery] int take = 50)
    {
        var importType = ParseType(type);
        var result = await _importService.GetUploadHistoryAsync(importType, take);
        return Ok(result);
    }

    // Audit log scoped to a single import job
    [HttpGet("{importJobId:int}/audit")]
    public async Task<ActionResult<List<AuditLogDto>>> AuditLog(string type, int importJobId)
    {
        var result = await _importService.GetAuditLogsAsync(importJobId, 200);
        return Ok(result);
    }
}
