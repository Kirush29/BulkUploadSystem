using BulkImportPlatform.Core.DTOs;
using BulkImportPlatform.Core.Enums;
using BulkImportPlatform.Core.Interfaces;

namespace BulkImportPlatform.Infrastructure.Services;

/// <summary>
/// Validation Engine (Business Layer) - implements section 5's critical-error and warning
/// rules for both Vehicle and Customer imports.
///
/// Vehicle critical errors: Vehicle Number missing, Vehicle Number already exists (duplicate
/// within the file or against existing data), file format invalid / corrupted (handled upstream
/// by the parser and surfaced as a job-level failure rather than a per-row rule).
/// Vehicle warnings: VIN / Make / Model / Year / Fuel information missing.
///
/// Customer critical errors: Customer Name missing, file invalid / unprocessable (job-level).
/// Customer warnings: Email / Phone / Address missing.
/// </summary>
public class ValidationEngine : IValidationEngine
{
    public List<ValidationResultDto> Validate(
        Dictionary<string, string?> mappedRow,
        ImportType type,
        Dictionary<string, int> vehicleNumberCountsInFile,
        HashSet<string> existingVehicleNumbers)
    {
        return type == ImportType.Vehicle
            ? ValidateVehicle(mappedRow, vehicleNumberCountsInFile, existingVehicleNumbers)
            : ValidateCustomer(mappedRow);
    }

    private static List<ValidationResultDto> ValidateVehicle(
        Dictionary<string, string?> row,
        Dictionary<string, int> vehicleNumberCountsInFile,
        HashSet<string> existingVehicleNumbers)
    {
        var results = new List<ValidationResultDto>();
        var vehicleNumber = GetValue(row, "VehicleNumber");

        // --- Critical errors ---
        if (string.IsNullOrWhiteSpace(vehicleNumber))
        {
            results.Add(Error("VehicleNumber", "VEHICLE_NUMBER_MISSING", "Vehicle Number is missing."));
        }
        else
        {
            var normalized = vehicleNumber.Trim().ToUpperInvariant();

            if (existingVehicleNumbers.Contains(normalized))
            {
                results.Add(Error("VehicleNumber", "VEHICLE_NUMBER_EXISTS", $"Vehicle Number '{vehicleNumber}' already exists."));
            }

            // Duplicate within the same file: flag any vehicle number that appears more than once.
            if (vehicleNumberCountsInFile.TryGetValue(normalized, out var occurrences) && occurrences > 1)
            {
                results.Add(Error("VehicleNumber", "VEHICLE_NUMBER_DUPLICATE_IN_FILE",
                    $"Vehicle Number '{vehicleNumber}' is duplicated elsewhere in this file."));
            }
        }

        // --- Warnings ---
        if (string.IsNullOrWhiteSpace(GetValue(row, "VIN")))
            results.Add(Warning("VIN", "VIN_MISSING", "VIN is missing."));

        if (string.IsNullOrWhiteSpace(GetValue(row, "Make")))
            results.Add(Warning("Make", "MAKE_MISSING", "Make is missing."));

        if (string.IsNullOrWhiteSpace(GetValue(row, "Model")))
            results.Add(Warning("Model", "MODEL_MISSING", "Model is missing."));

        if (string.IsNullOrWhiteSpace(GetValue(row, "Year")))
            results.Add(Warning("Year", "YEAR_MISSING", "Year is missing."));

        if (string.IsNullOrWhiteSpace(GetValue(row, "FuelType")))
            results.Add(Warning("FuelType", "FUEL_MISSING", "Fuel information is missing."));

        // Basic type-sanity checks (not enumerated explicitly in the proposal but implied by
        // "configurable validation" - surfaced as warnings so bad data doesn't silently import).
        var yearRaw = GetValue(row, "Year");
        if (!string.IsNullOrWhiteSpace(yearRaw) && !int.TryParse(yearRaw, out _))
            results.Add(Warning("Year", "YEAR_INVALID", $"Year '{yearRaw}' is not a valid number."));

        return results;
    }

    private static List<ValidationResultDto> ValidateCustomer(Dictionary<string, string?> row)
    {
        var results = new List<ValidationResultDto>();

        if (string.IsNullOrWhiteSpace(GetValue(row, "CustomerName")))
        {
            results.Add(Error("CustomerName", "CUSTOMER_NAME_MISSING", "Customer Name is missing."));
        }

        if (string.IsNullOrWhiteSpace(GetValue(row, "Email")))
            results.Add(Warning("Email", "EMAIL_MISSING", "Email is missing."));

        if (string.IsNullOrWhiteSpace(GetValue(row, "Phone")))
            results.Add(Warning("Phone", "PHONE_MISSING", "Phone number is missing."));

        if (string.IsNullOrWhiteSpace(GetValue(row, "Address")) &&
            string.IsNullOrWhiteSpace(GetValue(row, "City")) &&
            string.IsNullOrWhiteSpace(GetValue(row, "State")) &&
            string.IsNullOrWhiteSpace(GetValue(row, "Country")))
        {
            results.Add(Warning("Address", "ADDRESS_MISSING", "Address information is missing."));
        }

        var email = GetValue(row, "Email");
        if (!string.IsNullOrWhiteSpace(email) && !email.Contains('@'))
            results.Add(Warning("Email", "EMAIL_INVALID", $"Email '{email}' does not look like a valid email address."));

        return results;
    }

    private static string? GetValue(Dictionary<string, string?> row, string field) =>
        row.TryGetValue(field, out var value) ? value : null;

    private static ValidationResultDto Error(string field, string code, string message) => new()
    {
        Severity = ValidationSeverity.Error,
        FieldName = field,
        RuleCode = code,
        Message = message
    };

    private static ValidationResultDto Warning(string field, string code, string message) => new()
    {
        Severity = ValidationSeverity.Warning,
        FieldName = field,
        RuleCode = code,
        Message = message
    };
}
