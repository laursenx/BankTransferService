using BankTransferService.Models.Domain;
using BankTransferService.Models.Requests;

namespace BankTransferService.Interfaces;

/// <summary>
/// Business logic contract for transfer operations.
/// </summary>
public interface ITransferService
{
    /// <summary>
    /// Executes a transfer. When <paramref name="idempotencyKey"/> is provided and a transfer
    /// already exists with that key, the existing transfer is returned without creating a new one.
    /// </summary>
    Task<TransferResult> ExecuteTransferAsync(
        TransferRequest request,
        string? idempotencyKey = null
    );
}
