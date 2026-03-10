using System.Data.Common;
using BankTransferService.Interfaces;
using BankTransferService.Models.Domain;
using BankTransferService.Models.Requests;

namespace BankTransferService.Services;

/// <inheritdoc />
public class TransferService : ITransferService
{
    private readonly ITransferRepository _transferRepository;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<TransferService> _logger;

    public TransferService(
        ITransferRepository transferRepository,
        IDbConnectionFactory connectionFactory,
        ILogger<TransferService> logger
    )
    {
        _transferRepository = transferRepository;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<TransferResult> ExecuteTransferAsync(TransferRequest request)
    {
        if (request.Amount <= 0)
            return TransferResult.Fail("Amount must be greater than 0.");

        if (request.FromAccountId == request.ToAccountId)
            return TransferResult.Fail("Source and destination accounts must be different.");

        try
        {
            var transfer = new Transfer
            {
                FromAccountId = request.FromAccountId,
                ToAccountId = request.ToAccountId,
                Amount = request.Amount,
                Reference = request.Reference,
                Description = request.Description,
            };

            return await _transferRepository.ExecuteTransferAsync(transfer, _connectionFactory);
        }
        catch (DbException ex)
        {
            _logger.LogError(
                ex,
                "Database error executing transfer from {FromAccountId} to {ToAccountId}",
                request.FromAccountId,
                request.ToAccountId
            );
            return TransferResult.DatabaseError(
                "A database error occurred. The transfer was not completed."
            );
        }
    }
}
