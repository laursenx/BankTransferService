namespace BankTransferService.Models.Domain;

/// <summary>
/// Discrete outcomes of a transfer attempt. Used by the controller to map to HTTP status codes.
/// </summary>
public enum TransferStatus
{
    Success,
    ValidationFailed,
    NotFound,
    ServerError,
}
