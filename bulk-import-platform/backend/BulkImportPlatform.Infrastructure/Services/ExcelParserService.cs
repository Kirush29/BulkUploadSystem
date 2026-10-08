using BulkImportPlatform.Core;
using BulkImportPlatform.Core.Enums;
using BulkImportPlatform.Core.Interfaces;
using ClosedXML.Excel;

namespace BulkImportPlatform.Infrastructure.Services;

/// <summary>
/// File Processing Layer implementation (ClosedXML) per the proposal's system architecture.
/// </summary>
public class ExcelParserService : IExcelParserService
{
    public ParsedSheet Parse(Stream fileStream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (extension == ".csv")
            return ParseCsv(fileStream);

        if (extension != ".xlsx" && extension != ".xlsm")
            throw new InvalidDataException("File format is invalid. Only .xlsx, .xlsm, or .csv files are supported.");

        return ParseExcel(fileStream);
    }

    private static ParsedSheet ParseCsv(Stream fileStream)
    {
        using var reader = new StreamReader(fileStream);
        var firstLine = reader.ReadLine()
            ?? throw new InvalidDataException("The uploaded CSV file is empty.");

        var headers = firstLine.Split(',').Select(h => h.Trim().Trim('"')).Where(h => !string.IsNullOrWhiteSpace(h)).ToList();
        if (headers.Count == 0)
            throw new InvalidDataException("No column headers were detected in the first row of the CSV.");

        var result = new ParsedSheet { Headers = headers };
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var values = line.Split(',').Select(v => v.Trim().Trim('"')).ToArray();
            var rowDict = new Dictionary<string, string?>();
            var isEmptyRow = true;
            for (var i = 0; i < headers.Count; i++)
            {
                var value = i < values.Length ? values[i] : null;
                if (!string.IsNullOrEmpty(value)) isEmptyRow = false;
                rowDict[headers[i]] = string.IsNullOrEmpty(value) ? null : value;
            }
            if (!isEmptyRow) result.Rows.Add(rowDict);
        }
        return result;
    }

    private static ParsedSheet ParseExcel(Stream fileStream)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(fileStream);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("Uploaded file is corrupted or could not be parsed as an Excel workbook.", ex);
        }

        using (workbook)
        {
            var worksheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new InvalidDataException("The uploaded workbook does not contain any worksheets.");

            var usedRange = worksheet.RangeUsed()
                ?? throw new InvalidDataException("The uploaded worksheet is empty.");

            var firstRow = usedRange.FirstRow();
            var headers = new List<string>();
            foreach (var cell in firstRow.Cells())
            {
                var header = cell.GetString().Trim();
                if (!string.IsNullOrWhiteSpace(header))
                    headers.Add(header);
            }

            if (headers.Count == 0)
                throw new InvalidDataException("No column headers were detected in the first row of the worksheet.");

            var result = new ParsedSheet { Headers = headers };
            var dataRows = usedRange.RowsUsed().Skip(1);
            foreach (var row in dataRows)
            {
                var rowDict = new Dictionary<string, string?>();
                var isEmptyRow = true;
                for (var i = 0; i < headers.Count; i++)
                {
                    var cell = row.Cell(i + 1);
                    var value = cell.IsEmpty() ? null : cell.GetString().Trim();
                    if (!string.IsNullOrEmpty(value)) isEmptyRow = false;
                    rowDict[headers[i]] = string.IsNullOrEmpty(value) ? null : value;
                }
                if (!isEmptyRow) result.Rows.Add(rowDict);
            }
            return result;
        }
    }

    public byte[] GenerateTemplate(ImportType type)
    {
        using var workbook = new XLWorkbook();
        var sheetName = type == ImportType.Vehicle ? "Vehicles" : "Customers";
        var worksheet = workbook.Worksheets.Add(sheetName);

        var fields = FieldDefinitions.For(type);
        for (var i = 0; i < fields.Count; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = fields[i].SystemField + (fields[i].IsMandatory ? " *" : "");
            cell.Style.Font.Bold = true;
            if (fields[i].IsMandatory)
            {
                cell.Style.Fill.BackgroundColor = XLColor.LightYellow;
            }
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
