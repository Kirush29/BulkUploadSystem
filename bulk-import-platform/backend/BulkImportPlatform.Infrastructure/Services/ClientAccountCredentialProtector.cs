using BulkImportPlatform.Core.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace BulkImportPlatform.Infrastructure.Services;

/// <summary>
/// Encrypts client irentosoft passwords at rest using ASP.NET Core's Data Protection API.
/// The purpose string is versioned (".v1") so a future re-key/rotation strategy has a clean
/// seam. Key ring persistence (so encrypted passwords survive a redeploy) is configured at
/// startup in Program.cs - this class only performs Protect/Unprotect.
/// </summary>
public class ClientAccountCredentialProtector : IClientAccountCredentialProtector
{
    private readonly IDataProtector _protector;

    public ClientAccountCredentialProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("BulkImportPlatform.ClientAccountCredentials.v1");
    }

    public string Protect(string plaintextPassword) => _protector.Protect(plaintextPassword);

    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
