using BankTransferService.Models.Domain;

namespace BankTransferService.Interfaces;

/// <summary>
/// Data access contract for account read operations.
/// </summary>
public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id);
}
