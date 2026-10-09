using System.Globalization;

namespace BulkImportPlatform.Core;

/// <summary>
/// Parsing for mapped cell values, shared by validation and the irentosoft push so both always
/// agree on what a value means. Excel date cells arrive as ISO strings (see ExcelParserService);
/// CSV values can be in either invariant or the server's culture format.
/// </summary>
public static class ImportValueParsers
{
    public static DateTime? TryParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var invariant)) return invariant;
        if (DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out var local)) return local;
        return null;
    }

    public static decimal? TryParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim().TrimStart('$');
        return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    /// <summary>Last word = last name, everything before it = first name; a single word is a first name only.</summary>
    public static (string First, string? Last) SplitName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length <= 1) return (parts.FirstOrDefault() ?? string.Empty, null);
        return (string.Join(' ', parts[..^1]), parts[^1]);
    }
}
