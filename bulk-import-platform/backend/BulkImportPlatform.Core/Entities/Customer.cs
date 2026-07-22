namespace BulkImportPlatform.Core.Entities;

/// <summary>
/// Represents a customer record created either manually or through the bulk import platform.
/// Mandatory field per the proposal: CustomerName.
/// Optional fields: Email, Phone, Address, City, State, Country, DateOfBirth, DriverLicenseNumber.
/// </summary>
public class Customer
{
    public int Id { get; set; }

    // Mandatory
    public string CustomerName { get; set; } = string.Empty;

    // Optional
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? DriverLicenseNumber { get; set; }

    // Populated by default-value processing (Customer Status -> Active) when not supplied
    public string? Status { get; set; }

    // System metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }

    // Traceability back to the import that created this record
    public int? ImportJobId { get; set; }
    public ImportJob? ImportJob { get; set; }
    public int? ImportRecordId { get; set; }

    // Which client this customer belongs to (see Vehicle.ClientAccountId for the same rationale).
    public int? ClientAccountId { get; set; }
    public ClientAccount? ClientAccount { get; set; }
}
