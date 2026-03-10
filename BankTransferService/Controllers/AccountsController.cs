using BankTransferService.Interfaces;
using BankTransferService.Models.Responses;
using Microsoft.AspNetCore.Mvc;

namespace BankTransferService.Controllers;

/// <summary>
/// Handles account retrieval and transfer history endpoints.
/// </summary>
[ApiController]
[Route("api/accounts")]
public class AccountsController : ControllerBase
{
    private readonly IAccountRepository _accountRepository;
    private readonly ITransferQueryRepository _transferQueryRepository;

    public AccountsController(
        IAccountRepository accountRepository,
        ITransferQueryRepository transferQueryRepository
    )
    {
        _accountRepository = accountRepository;
        _transferQueryRepository = transferQueryRepository;
    }

    /// <summary>
    /// Returns account data for the given account id.
    /// </summary>
    /// <param name="id">Account GUID</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccount(Guid id)
    {
        var account = await _accountRepository.GetByIdAsync(id);
        if (account is null)
            return NotFound(new ErrorResponse { Message = $"Account '{id}' was not found." });

        return Ok(
            new AccountResponse
            {
                Id = account.Id,
                AccountNumber = account.AccountNumber,
                OwnerName = account.OwnerName,
                Balance = account.Balance,
            }
        );
    }

    /// <summary>
    /// Returns all transfers for a given account, newest first.
    /// </summary>
    /// <param name="id">Account GUID</param>
    [HttpGet("{id:guid}/transfers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransfers(Guid id)
    {
        var account = await _accountRepository.GetByIdAsync(id);
        if (account is null)
            return NotFound(new ErrorResponse { Message = $"Account '{id}' was not found." });

        var transfers = await _transferQueryRepository.GetByAccountIdAsync(id);
        return Ok(
            transfers.Select(t => new TransferResponse
            {
                Id = t.Id,
                FromAccountId = t.FromAccountId,
                ToAccountId = t.ToAccountId,
                Amount = t.Amount,
                Reference = t.Reference,
                Description = t.Description,
                CreatedUtc = t.CreatedUtc,
            })
        );
    }
}
