using BulkImportPlatform.Core.Enums;

namespace BulkImportPlatform.Core;

public class FieldDefinition
{
    public string SystemField { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public string DataType { get; set; } = "string"; // string | int | decimal | date
}

/// <summary>
/// Central catalogue of system fields for each import type, taken directly from
/// section 5 (Functional Requirements) of the Project Proposal. Used by the
/// mapping engine (to suggest/validate mappings), the validation engine (to know
/// which fields are mandatory), the default-value engine, and the Excel template generator.
/// </summary>
public static class FieldDefinitions
{
    public static readonly List<FieldDefinition> Vehicle = new()
    {
        new FieldDefinition { SystemField = "VehicleNumber", IsMandatory = true, DataType = "string" },
        new FieldDefinition { SystemField = "VIN", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "Make", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "Model", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "Year", IsMandatory = false, DataType = "int" },
        new FieldDefinition { SystemField = "Color", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "FuelType", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "TankSize", IsMandatory = false, DataType = "decimal" },
        new FieldDefinition { SystemField = "Odometer", IsMandatory = false, DataType = "decimal" },
        new FieldDefinition { SystemField = "LicenseNumber", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "SpotNumber", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "Transmission", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "VehicleType", IsMandatory = false, DataType = "string" },
    };

    public static readonly List<FieldDefinition> Customer = new()
    {
        new FieldDefinition { SystemField = "CustomerName", IsMandatory = true, DataType = "string" },
        new FieldDefinition { SystemField = "Email", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "Phone", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "Address", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "City", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "State", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "Country", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "DateOfBirth", IsMandatory = false, DataType = "date" },
        new FieldDefinition { SystemField = "DriverLicenseNumber", IsMandatory = false, DataType = "string" },
    };

    /// <summary>
    /// Reservations are pushed straight to irentosoft's booking API (see RpaImportService), so
    /// these mirror what that API needs: dates, a vehicle (by number, or by group/type when no
    /// specific vehicle is assigned), pickup/return location names, and the customer - matched
    /// by irentosoft on phone number, or created from the name when no match is found.
    /// </summary>
    public static readonly List<FieldDefinition> Reservation = new()
    {
        new FieldDefinition { SystemField = "ReservationNumber", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "PickupDateTime", IsMandatory = true, DataType = "date" },
        new FieldDefinition { SystemField = "ReturnDateTime", IsMandatory = true, DataType = "date" },
        new FieldDefinition { SystemField = "CustomerName", IsMandatory = true, DataType = "string" },
        new FieldDefinition { SystemField = "CustomerPhone", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "CustomerEmail", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "VehicleNumber", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "VehicleGroup", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "PickupLocation", IsMandatory = true, DataType = "string" },
        new FieldDefinition { SystemField = "ReturnLocation", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "Status", IsMandatory = false, DataType = "string" },
        new FieldDefinition { SystemField = "VehiclePrice", IsMandatory = false, DataType = "decimal" },
        new FieldDefinition { SystemField = "SecurityDeposit", IsMandatory = false, DataType = "decimal" },
        new FieldDefinition { SystemField = "Notes", IsMandatory = false, DataType = "string" },
    };

    public static List<FieldDefinition> For(ImportType type) => type switch
    {
        ImportType.Vehicle => Vehicle,
        ImportType.Reservation => Reservation,
        _ => Customer
    };

    public static Dictionary<string, string> BuiltInDefaultsFor(ImportType type) => type switch
    {
        ImportType.Vehicle => BuiltInDefaultsVehicle,
        ImportType.Reservation => BuiltInDefaultsReservation,
        _ => BuiltInDefaultsCustomer
    };

    /// <summary>
    /// Default heuristic mapping profile applied automatically when a spreadsheet's column
    /// headers already match (or closely match) system field names, so users don't have to
    /// manually map obvious columns. Users can override any mapping in the UI before validating.
    /// </summary>
    public static readonly Dictionary<string, string> CommonAliasHints = new(StringComparer.OrdinalIgnoreCase)
    {
        // Vehicle aliases (mirrors the "Manufacturer -> Make", "Registration -> License Number"
        // example from section 6 of the proposal)
        { "Manufacturer", "Make" },
        { "Registration", "LicenseNumber" },
        { "Vehicle No", "VehicleNumber" },
        { "Vehicle No.", "VehicleNumber" },
        { "Reg Number", "LicenseNumber" },
        { "Fuel", "FuelType" },
        { "Spot No", "SpotNumber" },

        // Customer aliases
        { "Name", "CustomerName" },
        { "Full Name", "CustomerName" },
        { "DOB", "DateOfBirth" },
        { "License Number", "DriverLicenseNumber" },
        { "Driving License", "DriverLicenseNumber" },
        { "Email Address", "Email" },
        { "Phone Number", "Phone" },
        { "Mobile", "Phone" },

        // Reservation aliases (headers of irentosoft-style reservation exports)
        { "Id", "ReservationNumber" },
        { "Reservation No", "ReservationNumber" },
        { "Booking Id", "ReservationNumber" },
        { "Pickup", "PickupDateTime" },
        { "Pickup Date", "PickupDateTime" },
        { "Start Date", "PickupDateTime" },
        { "Return", "ReturnDateTime" },
        { "Return Date", "ReturnDateTime" },
        { "End Date", "ReturnDateTime" },
        { "Customer", "CustomerName" },
        { "Customer Email", "CustomerEmail" },
        { "Vehicle Type", "VehicleGroup" },
        { "Notice", "Notes" },
        { "Note", "Notes" },
    };

    /// <summary>
    /// Default values applied by the Default Value Engine (section 7 of the proposal) when a
    /// mapping profile does not explicitly specify one and the field is missing on a row.
    /// </summary>
    public static readonly Dictionary<string, string> BuiltInDefaultsVehicle = new()
    {
        { "Year", "CURRENT_YEAR" },
        { "TankSize", "0" },
        { "Odometer", "0" },
    };

    public static readonly Dictionary<string, string> BuiltInDefaultsCustomer = new()
    {
        { "Status", "Active" },
    };

    // No built-ins: a reservation's Status is the source system's (reserved/done/cancel...)
    // and is used to choose which rows get pushed, so it must never be filled in silently.
    public static readonly Dictionary<string, string> BuiltInDefaultsReservation = new();
}
