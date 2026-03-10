namespace BankTransferService.Models.Domain;

/// <summary>
/// Result returned from the service layer to the controller after a transfer attempt.
/// </summary>
public class TransferResult
{
    public bool Success { get; private set; }
    public bool IsNotFound { get; private set; }
    public bool IsServerError { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid? TransferId { get; private set; }

    private TransferResult() { }

    public static TransferResult Ok(Guid transferId) =>
        new TransferResult { Success = true, TransferId = transferId };

    public static TransferResult Fail(string errorMessage) =>
        new TransferResult { Success = false, ErrorMessage = errorMessage };

    public static TransferResult NotFound(string errorMessage) =>
        new TransferResult
        {
            Success = false,
            IsNotFound = true,
            ErrorMessage = errorMessage,
        };

    public static TransferResult DatabaseError(string errorMessage) =>
        new TransferResult
        {
            Success = false,
            IsServerError = true,
            ErrorMessage = errorMessage,
        };
}
