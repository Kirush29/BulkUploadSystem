using BulkImportPlatform.Core.DTOs;
using BulkImportPlatform.Core.Entities;
using BulkImportPlatform.Core.Interfaces;
using BulkImportPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BulkImportPlatform.Infrastructure.Services;

/// <summary>
/// CRUD for the per-client irentosoft login records used by the RPA push step.
/// Passwords are write-only: ClientAccountDto never carries one, so a plaintext
/// password can never round-trip back out of this service once stored.
/// </summary>
public class ClientAccountService : IClientAccountService
{
    private readonly BulkImportDbContext _db;
    private readonly IClientAccountCredentialProtector _protector;

    public ClientAccountService(BulkImportDbContext db, IClientAccountCredentialProtector protector)
    {
        _db = db;
        _protector = protector;
    }

    public async Task<List<ClientAccountDto>> ListAsync()
    {
        return await _db.ClientAccounts
            .OrderBy(c => c.Name)
            .Select(c => ToDto(c))
            .ToListAsync();
    }

    public async Task<ClientAccountDto> GetAsync(int id)
    {
        var account = await Find(id);
        return ToDto(account);
    }

    public async Task<ClientAccountDto> CreateAsync(CreateClientAccountRequestDto request, string performedBy)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Client account name is required.");
        if (string.IsNullOrWhiteSpace(request.IrentosoftUsername))
            throw new ArgumentException("irentosoft username is required.");
        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("irentosoft password is required.");

        var account = new ClientAccount
        {
            Name = request.Name.Trim(),
            IrentosoftUsername = request.IrentosoftUsername.Trim(),
            EncryptedPassword = _protector.Protect(request.Password),
            IsActive = true,
            CreatedBy = performedBy
        };
        _db.ClientAccounts.Add(account);
        await _db.SaveChangesAsync();

        return ToDto(account);
    }

    public async Task<ClientAccountDto> UpdateAsync(int id, UpdateClientAccountRequestDto request, string performedBy)
    {
        var account = await Find(id);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Client account name is required.");
        if (string.IsNullOrWhiteSpace(request.IrentosoftUsername))
            throw new ArgumentException("irentosoft username is required.");

        account.Name = request.Name.Trim();
        account.IrentosoftUsername = request.IrentosoftUsername.Trim();
        account.IsActive = request.IsActive;
        account.UpdatedAt = DateTime.UtcNow;
        account.UpdatedBy = performedBy;

        await _db.SaveChangesAsync();
        return ToDto(account);
    }

    public async Task ResetPasswordAsync(int id, ResetClientAccountPasswordRequestDto request, string performedBy)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("A new password is required.");

        var account = await Find(id);
        account.EncryptedPassword = _protector.Protect(request.Password);
        account.UpdatedAt = DateTime.UtcNow;
        account.UpdatedBy = performedBy;

        await _db.SaveChangesAsync();
    }

    private async Task<ClientAccount> Find(int id) =>
        await _db.ClientAccounts.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException($"Client account {id} was not found.");

    private static ClientAccountDto ToDto(ClientAccount c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        IrentosoftUsername = c.IrentosoftUsername,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt,
        CreatedBy = c.CreatedBy
    };
}
