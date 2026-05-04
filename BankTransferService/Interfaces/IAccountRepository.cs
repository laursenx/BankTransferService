using System.Data.Common;
using BankTransferService.Models.Domain;

namespace BankTransferService.Interfaces;

/// <summary>
/// Data access contract for account operations.
/// </summary>
public interface IAccountRepository
{
    /// <summary>
    /// Reads an account without acquiring locks. Used for read-only queries.
    /// </summary>
    Task<Account?> GetByIdAsync(Guid id);

    /// <summary>
    /// Reads an account with row-level update locks held until the supplied transaction commits or rolls back.
    /// Use inside a transfer transaction so the row cannot change between validation and update.
    /// </summary>
    Task<Account?> GetByIdForUpdateAsync(
        Guid id,
        DbConnection connection,
        DbTransaction transaction
    );

    /// <summary>
    /// Adjusts an account's balance by the given delta inside the supplied transaction.
    /// </summary>
    Task UpdateBalanceAsync(
        Guid id,
        decimal delta,
        DbConnection connection,
        DbTransaction transaction
    );
}
