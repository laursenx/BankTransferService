namespace BankTransferService.Models.Responses;

/// <summary>
/// Response returned when an account is retrieved.
/// Exposes only the fields relevant to the API caller.
/// </summary>
public class AccountResponse
{
    public Guid Id { get; init; }
    public string AccountNumber { get; init; } = string.Empty;
    public string OwnerName { get; init; } = string.Empty;
    public decimal Balance { get; init; }
}
