namespace BankTransferService.Models.Domain;

/// <summary>
/// Outcome of a transfer attempt. Discriminated by <see cref="Status"/> so callers
/// switch on a single value rather than a bag of bool flags.
/// </summary>
public class TransferResult
{
    public TransferStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid? TransferId { get; private set; }

    public bool Success => Status == TransferStatus.Success;

    private TransferResult() { }

    public static TransferResult Ok(Guid transferId) =>
        new TransferResult { Status = TransferStatus.Success, TransferId = transferId };

    public static TransferResult Fail(string errorMessage) =>
        new TransferResult
        {
            Status = TransferStatus.ValidationFailed,
            ErrorMessage = errorMessage,
        };

    public static TransferResult NotFound(string errorMessage) =>
        new TransferResult { Status = TransferStatus.NotFound, ErrorMessage = errorMessage };

    public static TransferResult DatabaseError(string errorMessage) =>
        new TransferResult { Status = TransferStatus.ServerError, ErrorMessage = errorMessage };
}
