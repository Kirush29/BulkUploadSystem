namespace BulkImportPlatform.Core.Entities;

/// <summary>
/// A client's login credentials for the separate, external irentosoft system.
/// Used by the RPA automation pipeline to log in as that specific client and
/// enter their Vehicle/Customer records through irentosoft's own web forms.
/// </summary>
public class ClientAccount
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string IrentosoftUsername { get; set; } = string.Empty;

    // Ciphertext produced by IClientAccountCredentialProtector.Protect() - never the raw password.
    public string EncryptedPassword { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
