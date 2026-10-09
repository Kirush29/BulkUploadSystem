using BulkImportPlatform.Core;
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
/// by the parser and surfaced as a job-level failure rather than a per-row rule). A number that
/// only exists from an earlier import that never reached irentosoft is a warning, not an error -
/// committing replaces that stale local copy (see ImportService.CommitImportAsync).
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
        HashSet<string> existingVehicleNumbers,
        Dictionary<string, int> unpushedVehicleNumbers)
    {
        return type == ImportType.Vehicle
            ? ValidateVehicle(mappedRow, vehicleNumberCountsInFile, existingVehicleNumbers, unpushedVehicleNumbers)
            : ValidateCustomer(mappedRow);
    }

    private static List<ValidationResultDto> ValidateVehicle(
        Dictionary<string, string?> row,
        Dictionary<string, int> vehicleNumberCountsInFile,
        HashSet<string> existingVehicleNumbers,
        Dictionary<string, int> unpushedVehicleNumbers)
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
            else if (unpushedVehicleNumbers.TryGetValue(normalized, out var earlierJobId))
            {
                results.Add(Warning("VehicleNumber", "VEHICLE_NUMBER_NOT_PUSHED",
                    $"Vehicle Number '{vehicleNumber}' was imported in job #{earlierJobId} but never reached irentosoft - importing it again replaces that earlier copy."));
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

        // Not mandatory for the local import, but irentosoft refuses to save a vehicle without
        // one - flag it here so it can be mapped/defaulted before the push, not discovered
        // row by row during it.
        if (string.IsNullOrWhiteSpace(GetValue(row, "VehicleType")))
            results.Add(Warning("VehicleType", "VEHICLE_TYPE_MISSING",
                "Vehicle Type is missing - irentosoft requires it, so this row will fail when pushed. Map a column or set a default on the Mapping step."));

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

    /// <summary>
    /// Reservation critical errors are the things irentosoft's booking API itself rejects
    /// (missing/invalid dates, return less than an hour after pickup, no pickup location, no
    /// vehicle or vehicle group to book), plus duplicate reservation numbers. Whether the
    /// vehicle/location names actually exist in the account is only known at push time.
    /// </summary>
    public List<ValidationResultDto> ValidateReservation(
        Dictionary<string, string?> row,
        Dictionary<string, int> reservationNumberCountsInFile,
        Dictionary<string, int> pushedReservationNumbers)
    {
        var results = new List<ValidationResultDto>();

        var reservationNumber = GetValue(row, "ReservationNumber");
        if (!string.IsNullOrWhiteSpace(reservationNumber))
        {
            var normalized = reservationNumber.Trim().ToUpperInvariant();
            if (pushedReservationNumbers.TryGetValue(normalized, out var earlierJobId))
                results.Add(Error("ReservationNumber", "RESERVATION_ALREADY_PUSHED",
                    $"Reservation '{reservationNumber}' was already pushed to irentosoft by job #{earlierJobId}."));

            if (reservationNumberCountsInFile.TryGetValue(normalized, out var occurrences) && occurrences > 1)
                results.Add(Error("ReservationNumber", "RESERVATION_DUPLICATE_IN_FILE",
                    $"Reservation '{reservationNumber}' is duplicated elsewhere in this file."));
        }

        var pickupRaw = GetValue(row, "PickupDateTime");
        var returnRaw = GetValue(row, "ReturnDateTime");
        var pickup = ImportValueParsers.TryParseDateTime(pickupRaw);
        var returnAt = ImportValueParsers.TryParseDateTime(returnRaw);

        if (string.IsNullOrWhiteSpace(pickupRaw))
            results.Add(Error("PickupDateTime", "PICKUP_MISSING", "Pickup date/time is missing."));
        else if (pickup == null)
            results.Add(Error("PickupDateTime", "PICKUP_INVALID", $"Pickup '{pickupRaw}' is not a valid date/time."));

        if (string.IsNullOrWhiteSpace(returnRaw))
            results.Add(Error("ReturnDateTime", "RETURN_MISSING", "Return date/time is missing."));
        else if (returnAt == null)
            results.Add(Error("ReturnDateTime", "RETURN_INVALID", $"Return '{returnRaw}' is not a valid date/time."));

        if (pickup != null && returnAt != null && returnAt.Value < pickup.Value.AddHours(1))
            results.Add(Error("ReturnDateTime", "RETURN_TOO_EARLY", "Return must be at least 1 hour after pickup (irentosoft rule)."));

        var customerName = GetValue(row, "CustomerName");
        if (string.IsNullOrWhiteSpace(customerName))
            results.Add(Error("CustomerName", "CUSTOMER_NAME_MISSING", "Customer name is missing."));
        else if (!customerName.Trim().Contains(' '))
            results.Add(Warning("CustomerName", "CUSTOMER_LAST_NAME_MISSING", $"'{customerName}' has no last name - it will be entered as a first name only."));

        if (string.IsNullOrWhiteSpace(GetValue(row, "PickupLocation")))
            results.Add(Error("PickupLocation", "PICKUP_LOCATION_MISSING", "Pickup location is missing - map a column or set a default on the Mapping step."));

        if (string.IsNullOrWhiteSpace(GetValue(row, "VehicleNumber")) && string.IsNullOrWhiteSpace(GetValue(row, "VehicleGroup")))
            results.Add(Error("VehicleNumber", "VEHICLE_MISSING", "Neither a vehicle number nor a vehicle group is given."));

        var phone = GetValue(row, "CustomerPhone");
        if (string.IsNullOrWhiteSpace(phone))
            results.Add(Warning("CustomerPhone", "PHONE_MISSING", "Customer phone is missing - irentosoft matches existing customers by phone, so a new customer record will be created."));
        else if (phone.Trim().Length > 20)
            results.Add(Warning("CustomerPhone", "PHONE_TOO_LONG", $"Phone '{phone}' is longer than irentosoft's 20-character limit and will be cut short."));

        var email = GetValue(row, "CustomerEmail");
        if (!string.IsNullOrWhiteSpace(email) && !email.Contains('@'))
            results.Add(Warning("CustomerEmail", "EMAIL_INVALID", $"Email '{email}' does not look valid and will not be sent."));

        var priceRaw = GetValue(row, "VehiclePrice");
        if (string.IsNullOrWhiteSpace(priceRaw))
            results.Add(Warning("VehiclePrice", "PRICE_MISSING", "Vehicle price is missing - the rate plan's daily rate will be used."));
        else if (ImportValueParsers.TryParseDecimal(priceRaw) == null)
            results.Add(Warning("VehiclePrice", "PRICE_INVALID", $"Vehicle price '{priceRaw}' is not a number - the rate plan's daily rate will be used."));

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
