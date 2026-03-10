using BankTransferService.Models.Domain;

namespace BankTransferService.Interfaces;

/// <summary>
/// Data access contract for transfer operations.
/// </summary>
public interface ITransferRepository
{
    /// <summary>
    /// Reads both accounts with row-level locks, validates existence / active status / balance,
    /// then executes the debit, credit and transfer log insert atomically.
    /// </summary>
    Task<TransferResult> ExecuteTransferAsync(
        Transfer transfer,
        IDbConnectionFactory connectionFactory
    );
}
