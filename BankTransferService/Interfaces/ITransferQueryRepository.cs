using BankTransferService.Models;

namespace BankTransferService.Interfaces;

/// <summary>
/// Data access contract for querying transfer history.
/// </summary>
public interface ITransferQueryRepository
{
    /// <summary>
    /// Returns all transfers where the account is either sender or receiver, newest first.
    /// </summary>
    Task<IEnumerable<Transfer>> GetByAccountIdAsync(Guid accountId);
}
