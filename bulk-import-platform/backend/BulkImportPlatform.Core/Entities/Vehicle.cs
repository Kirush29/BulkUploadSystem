namespace BulkImportPlatform.Core.Entities;

/// <summary>
/// Represents a vehicle record created either manually or through the bulk import platform.
/// Mandatory field per the proposal: VehicleNumber.
/// Optional fields: VIN, Make, Model, Year, Color, FuelType, TankSize, Odometer,
/// LicenseNumber, SpotNumber, Transmission, VehicleType.
/// </summary>
public class Vehicle
{
    public int Id { get; set; }

    // Mandatory
    public string VehicleNumber { get; set; } = string.Empty;

    // Optional
    public string? VIN { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public string? Color { get; set; }
    public string? FuelType { get; set; }
    public decimal? TankSize { get; set; }
    public decimal? Odometer { get; set; }
    public string? LicenseNumber { get; set; }
    public string? SpotNumber { get; set; }
    public string? Transmission { get; set; }
    public string? VehicleType { get; set; }

    // System metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }

    // Traceability back to the import that created this record
    public int? ImportJobId { get; set; }
    public ImportJob? ImportJob { get; set; }
    public int? ImportRecordId { get; set; }

    // Which client this vehicle belongs to. VehicleNumber is unique per client, not globally -
    // two different clients may legitimately use the same vehicle number.
    public int? ClientAccountId { get; set; }
    public ClientAccount? ClientAccount { get; set; }
}
