using System.Data;
using System.Data.Common;
using System.Globalization;
using BankTransferService.Interfaces;
using BankTransferService.Models.Domain;
using BankTransferService.Models.Requests;

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

    public async Task<TransferResult> ExecuteTransferAsync(
        TransferRequest request,
        string? idempotencyKey = null
    )
    {
        if (request.Amount <= 0)
            return TransferResult.Fail("Amount must be greater than 0.");

        if (request.FromAccountId == request.ToAccountId)
            return TransferResult.Fail("Source and destination accounts must be different.");

        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync(
                IsolationLevel.RepeatableRead
            );

            if (idempotencyKey is not null)
            {
                var existingTransferId = await _transferRepository.FindIdByIdempotencyKeyAsync(
                    idempotencyKey,
                    connection,
                    transaction
                );
                if (existingTransferId.HasValue)
                    return TransferResult.Ok(existingTransferId.Value);
            }

            // Acquire row locks in a deterministic global order (lowest Guid first) so two
            // concurrent transfers in opposing directions (A->B and B->A) can never form a
            // deadlock cycle. With per-request ordering SQL Server detects the cycle and
            // kills one transaction with error 1205; with ordered locking the second
            // transfer just waits for the first to commit.
            var (firstId, secondId) =
                request.FromAccountId.CompareTo(request.ToAccountId) < 0
                    ? (request.FromAccountId, request.ToAccountId)
                    : (request.ToAccountId, request.FromAccountId);

            var firstAccount = await _accountRepository.GetByIdForUpdateAsync(
                firstId,
                connection,
                transaction
            );
            var secondAccount = await _accountRepository.GetByIdForUpdateAsync(
                secondId,
                connection,
                transaction
            );

            var fromAccount = firstId == request.FromAccountId ? firstAccount : secondAccount;
            var toAccount = firstId == request.FromAccountId ? secondAccount : firstAccount;

            if (fromAccount is null)
                return TransferResult.NotFound(
                    $"Source account '{request.FromAccountId}' was not found."
                );

            if (toAccount is null)
                return TransferResult.NotFound(
                    $"Destination account '{request.ToAccountId}' was not found."
                );

            var validationFailure = ValidateBusinessRules(fromAccount, toAccount, request.Amount);
            if (validationFailure is not null)
                return validationFailure;

            var transfer = new Transfer
            {
                Id = Guid.NewGuid(),
                FromAccountId = request.FromAccountId,
                ToAccountId = request.ToAccountId,
                Amount = request.Amount,
                Reference = request.Reference,
                Description = request.Description,
                IdempotencyKey = idempotencyKey,
                CreatedUtc = DateTime.UtcNow,
            };

            await _accountRepository.UpdateBalanceAsync(
                request.FromAccountId,
                -request.Amount,
                connection,
                transaction
            );
            await _accountRepository.UpdateBalanceAsync(
                request.ToAccountId,
                +request.Amount,
                connection,
                transaction
            );
            await _transferRepository.InsertAsync(transfer, connection, transaction);

            await transaction.CommitAsync();

            return TransferResult.Ok(transfer.Id);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            // ADO.NET surfaces failures both as DbException (SQL errors, deadlock
            // victims, timeouts) and as InvalidOperationException (connection in
            // wrong state, transaction already disposed, command misuse). Both
            // need to roll back the ambient transaction and return a clean 500
            // instead of bubbling out as an unhandled exception.
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

    /// <summary>
    /// Pure business rules that depend on the loaded account state. Kept side-effect-free
    /// so it can be exercised directly from unit tests without any database setup.
    /// </summary>
    internal static TransferResult? ValidateBusinessRules(
        Account fromAccount,
        Account toAccount,
        decimal amount
    )
    {
        if (!fromAccount.IsActive)
            return TransferResult.Fail(
                $"Source account '{fromAccount.AccountNumber}' is not active."
            );

        if (!toAccount.IsActive)
            return TransferResult.Fail(
                $"Destination account '{toAccount.AccountNumber}' is not active."
            );

        var availableBalance = fromAccount.Balance + fromAccount.OverdraftLimit;
        if (amount > availableBalance)
            return TransferResult.Fail(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Insufficient funds. Available balance including overdraft: {availableBalance:F2}."
                )
            );

        return null;
    }
}
