using BulkImportPlatform.Core.DTOs;
using BulkImportPlatform.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BulkImportPlatform.Api.Controllers;

/// <summary>
/// Manages the per-client irentosoft login records used by the RPA push-to-irentosoft step.
/// Passwords are write-only: create/reset-password accept one, nothing else ever returns one.
/// </summary>
[ApiController]
[Route("api/client-accounts")]
public class ClientAccountsController : ControllerBase
{
    private readonly IClientAccountService _clientAccounts;

    public ClientAccountsController(IClientAccountService clientAccounts)
    {
        _clientAccounts = clientAccounts;
    }

    private string CurrentUser => Request.Headers.TryGetValue("X-User", out var user) && !string.IsNullOrWhiteSpace(user)
        ? user.ToString()
        : "demo-user";

    [HttpGet]
    public async Task<ActionResult<List<ClientAccountDto>>> List()
    {
        return Ok(await _clientAccounts.ListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientAccountDto>> Get(int id)
    {
        try
        {
            return Ok(await _clientAccounts.GetAsync(id));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<ClientAccountDto>> Create([FromBody] CreateClientAccountRequestDto request)
    {
        try
        {
            var result = await _clientAccounts.CreateAsync(request, CurrentUser);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ClientAccountDto>> Update(int id, [FromBody] UpdateClientAccountRequestDto request)
    {
        try
        {
            return Ok(await _clientAccounts.UpdateAsync(id, request, CurrentUser));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetClientAccountPasswordRequestDto request)
    {
        try
        {
            await _clientAccounts.ResetPasswordAsync(id, request, CurrentUser);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
