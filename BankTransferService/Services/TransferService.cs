using BankTransferService.Interfaces;
using BankTransferService.Models.Domain;
using BankTransferService.Models.Requests;
using Microsoft.Data.SqlClient;

namespace BankTransferService.Services;

/// <inheritdoc />
public class TransferService : ITransferService
{
    private readonly IAccountRepository _accountRepository;
    private readonly ITransferRepository _transferRepository;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<TransferService> _logger;

    public TransferService(
        IAccountRepository accountRepository,
        ITransferRepository transferRepository,
        IDbConnectionFactory connectionFactory,
        ILogger<TransferService> logger
    )
    {
        _accountRepository = accountRepository;
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

        var fromAccount = await _accountRepository.GetByIdAsync(request.FromAccountId);
        if (fromAccount is null)
            return TransferResult.NotFound(
                $"Source account '{request.FromAccountId}' was not found."
            );

        var toAccount = await _accountRepository.GetByIdAsync(request.ToAccountId);
        if (toAccount is null)
            return TransferResult.NotFound(
                $"Destination account '{request.ToAccountId}' was not found."
            );

        if (!fromAccount.IsActive)
            return TransferResult.Fail(
                $"Source account '{fromAccount.AccountNumber}' is not active."
            );

        if (!toAccount.IsActive)
            return TransferResult.Fail(
                $"Destination account '{toAccount.AccountNumber}' is not active."
            );

        var availableBalance = fromAccount.Balance + fromAccount.OverdraftLimit;
        if (request.Amount > availableBalance)
            return TransferResult.Fail(
                $"Insufficient funds. Available balance including overdraft: {availableBalance:F2}."
            );

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

            var transferId = await _transferRepository.ExecuteTransferAsync(
                transfer,
                _connectionFactory
            );
            return TransferResult.Ok(transferId);
        }
        catch (SqlException ex)
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
