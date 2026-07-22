using BulkImportPlatform.Core.DTOs;
using BulkImportPlatform.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BulkImportPlatform.Api.Controllers;

/// <summary>Platform Feature: Audit logging (section 4 / NFR: Auditability).</summary>
[ApiController]
[Route("api/audit-logs")]
public class AuditController : ControllerBase
{
    private readonly IImportService _importService;

    public AuditController(IImportService importService)
    {
        _importService = importService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AuditLogDto>>> List([FromQuery] int? importJobId, [FromQuery] int take = 200)
    {
        return Ok(await _importService.GetAuditLogsAsync(importJobId, take));
    }
}
