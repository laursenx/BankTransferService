using System.Data.Common;
using BankTransferService.Models.Domain;

namespace BankTransferService.Interfaces;

/// <summary>
/// Data access contract for writing transfers. Read queries live on <see cref="ITransferQueryRepository"/>.
/// </summary>
public interface ITransferRepository
{
    /// <summary>
    /// Inserts a transfer log row inside the supplied transaction.
    /// </summary>
    Task InsertAsync(Transfer transfer, DbConnection connection, DbTransaction transaction);

    /// <summary>
    /// Looks up an existing transfer by idempotency key inside the supplied transaction.
    /// Returns the transfer id if a row already exists, otherwise null.
    /// </summary>
    Task<Guid?> FindIdByIdempotencyKeyAsync(
        string idempotencyKey,
        DbConnection connection,
        DbTransaction transaction
    );
}
