using BulkImportPlatform.Core.Enums;
using BulkImportPlatform.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BulkImportPlatform.Api.Controllers;

/// <summary>
/// Excel Template Management (section 4: Platform Features - Template management).
/// </summary>
[ApiController]
[Route("api/templates")]
public class TemplatesController : ControllerBase
{
    private readonly IExcelParserService _excelService;

    public TemplatesController(IExcelParserService excelService)
    {
        _excelService = excelService;
    }

    [HttpGet("{type}")]
    public IActionResult Download(string type)
    {
        var importType = type.Trim().ToLowerInvariant() switch
        {
            "vehicle" or "vehicles" => ImportType.Vehicle,
            "customer" or "customers" => ImportType.Customer,
            "reservation" or "reservations" => ImportType.Reservation,
            _ => throw new ArgumentException($"Unknown import type '{type}'.")
        };

        var bytes = _excelService.GenerateTemplate(importType);
        var fileName = $"{importType.ToString().ToLowerInvariant()}_import_template.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
