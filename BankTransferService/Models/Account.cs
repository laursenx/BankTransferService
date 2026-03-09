namespace BankTransferService.Models;

/// <summary>
/// Represents a bank account that can send or receive money.
/// </summary>
public class Account
{
    public Guid Id { get; init; }
    public string AccountNumber { get; init; } = string.Empty;
    public string OwnerName { get; init; } = string.Empty;
    public decimal Balance { get; init; }
    public decimal OverdraftLimit { get; init; }
    public bool IsActive { get; init; }
}
