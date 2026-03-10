namespace BankTransferService.Models.Responses;

/// <summary>
/// Standard error response returned by the API.
/// </summary>
public class ErrorResponse
{
    public string Message { get; init; } = string.Empty;
}
