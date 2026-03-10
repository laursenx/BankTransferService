namespace BankTransferService.Models.Responses;

/// <summary>
/// Response returned when a transfer is successfully created.
/// </summary>
public class TransferCreatedResponse
{
    public Guid TransferId { get; init; }
}
