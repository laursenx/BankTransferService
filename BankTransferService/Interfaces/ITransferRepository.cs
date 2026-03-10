using BankTransferService.Models.Domain;

namespace BankTransferService.Interfaces;

/// <summary>
/// Data access contract for transfer operations.
/// </summary>
public interface ITransferRepository
{
    /// <summary>
    /// Executes the debit, credit and transfer log insert atomically using the provided connection factory.
    /// </summary>
    Task<Guid> ExecuteTransferAsync(Transfer transfer, IDbConnectionFactory connectionFactory);
}
