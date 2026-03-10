using BankTransferService.Models.Domain;
using BankTransferService.Models.Requests;

namespace BankTransferService.Interfaces;

/// <summary>
/// Business logic contract for transfer operations.
/// </summary>
public interface ITransferService
{
    Task<TransferResult> ExecuteTransferAsync(TransferRequest request);
}
