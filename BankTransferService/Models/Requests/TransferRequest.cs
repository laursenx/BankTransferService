using System.ComponentModel.DataAnnotations;

namespace BankTransferService.Models.Requests;

/// <summary>
/// Input model received from the API client when requesting a transfer.
/// </summary>
public class TransferRequest
{
    public Guid FromAccountId { get; set; }

    public Guid ToAccountId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(140)]
    public string Reference { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Description { get; set; }
}
