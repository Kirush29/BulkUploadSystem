using BulkImportPlatform.Core.DTOs;
using BulkImportPlatform.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BulkImportPlatform.Api.Controllers;

/// <summary>Platform Feature: Dashboard (section 4).</summary>
[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IImportService _importService;

    public DashboardController(IImportService importService)
    {
        _importService = importService;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> Summary()
    {
        return Ok(await _importService.GetDashboardSummaryAsync());
    }
}
