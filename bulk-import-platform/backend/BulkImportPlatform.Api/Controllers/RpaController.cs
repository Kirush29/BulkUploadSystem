using BulkImportPlatform.Core.DTOs;
using BulkImportPlatform.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BulkImportPlatform.Api.Controllers;

/// <summary>
/// Drives the "push to irentosoft" step: after IImportService.CommitImportAsync has already
/// created local Vehicle/Customer rows, this runs browser automation to enter them into
/// irentosoft under a specific client's login. Kept as a separate controller (not folded into
/// ImportsController) since RPA is cross-cutting to both Vehicle and Customer import types.
/// </summary>
[ApiController]
[Route("api/rpa")]
public class RpaController : ControllerBase
{
    private readonly IRpaImportService _rpaImportService;

    public RpaController(IRpaImportService rpaImportService)
    {
        _rpaImportService = rpaImportService;
    }

    private string CurrentUser => Request.Headers.TryGetValue("X-User", out var user) && !string.IsNullOrWhiteSpace(user)
        ? user.ToString()
        : "demo-user";

    // A run can take minutes for larger batches - kick it off and let the frontend poll /status
    // rather than holding the HTTP request open for the whole duration.
    [HttpPost("{importJobId:int}/run")]
    public async Task<ActionResult<RpaRunSummaryDto>> Run(int importJobId, [FromBody] RpaRunRequestDto request)
    {
        try
        {
            var options = request.Options ?? new RpaRunOptionsDto();
            var result = await _rpaImportService.RunAsync(
                importJobId, request.OwningLocation, request.CurrentLocation, CurrentUser, options);
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

    [HttpPost("{importJobId:int}/retry")]
    public async Task<ActionResult<RpaRunSummaryDto>> Retry(int importJobId, [FromBody] RpaRetryRequestDto request)
    {
        try
        {
            var options = request.Options ?? new RpaRunOptionsDto();
            var result = await _rpaImportService.RetryRecordsAsync(importJobId, request.ImportRecordIds, CurrentUser, options);
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

    // Corrects field(s) on a failed record (e.g. VehicleType/Model text that didn't match
    // irentosoft's configured options) and immediately retries just that record - avoids the
    // old workaround of re-uploading a whole new file with different vehicle numbers.
    [HttpPut("{importJobId:int}/records/{importRecordId:int}")]
    public async Task<ActionResult<RpaRunSummaryDto>> UpdateRecordAndRetry(int importJobId, int importRecordId, [FromBody] UpdateRecordAndRetryRequestDto request)
    {
        try
        {
            var options = request.Options ?? new RpaRunOptionsDto();
            var result = await _rpaImportService.UpdateRecordAndRetryAsync(importJobId, importRecordId, request.FieldUpdates, CurrentUser, options);
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

    [HttpGet("{importJobId:int}/status")]
    public async Task<ActionResult<RpaRunSummaryDto>> Status(int importJobId)
    {
        try
        {
            return Ok(await _rpaImportService.GetRpaStatusAsync(importJobId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // Serves a failure screenshot captured at the moment a dropdown/save failed (see
    // RpaImportService.CaptureScreenshotAsync). Path.GetFileName strips any directory
    // component from the request before touching the filesystem, so this can't be used to
    // read arbitrary files outside the screenshots folder.
    [HttpGet("screenshots/{fileName}")]
    public IActionResult Screenshot(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        var dir = Path.Combine(AppContext.BaseDirectory, "RpaEvidence", "Screenshots");
        var path = Path.Combine(dir, safeName);

        if (!System.IO.File.Exists(path))
        {
            return NotFound();
        }

        return PhysicalFile(path, "image/png");
    }
}
