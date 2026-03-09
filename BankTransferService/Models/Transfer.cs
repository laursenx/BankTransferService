namespace BankTransferService.Models;

/// <summary>
/// Represents a completed money transfer between two accounts.
/// </summary>
public class Transfer
{
    public Guid Id { get; init; }
    public Guid FromAccountId { get; init; }
    public Guid ToAccountId { get; init; }
    public decimal Amount { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime CreatedUtc { get; init; }
}
