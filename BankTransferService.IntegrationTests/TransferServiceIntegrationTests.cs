using System.Data.Common;
using BankTransferService.Data;
using BankTransferService.Interfaces;
using BankTransferService.Models.Domain;
using BankTransferService.Models.Requests;
using BankTransferService.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

namespace BankTransferService.IntegrationTests;

/// <summary>
/// End-to-end tests for <see cref="TransferService"/> against a real SQL Server.
///
/// These tests cover concerns that cannot be honestly tested with mocks:
/// row-level locking, transaction commit, and rollback behaviour when something fails
/// after balances have been touched.
/// </summary>
public class TransferServiceIntegrationTests : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    // Account ids match the seed data in bank-transfer-service-schema-seed.sql.
    private static readonly Guid Account1001 = new("11111111-1111-1111-1111-111111111111"); // 5000.00, active
    private static readonly Guid Account1002 = new("22222222-2222-2222-2222-222222222222"); // 1250.00, active
    private static readonly Guid Account2001 = new("33333333-3333-3333-3333-333333333333"); //  150.00 + 200 overdraft, active
    private static readonly Guid Account9001 = new("99999999-9999-9999-9999-999999999999"); //  800.00, inactive

    private readonly DatabaseFixture _fixture;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IAccountRepository _accountRepository;
    private readonly ITransferRepository _transferRepository;
    private readonly TransferService _sut;

    public TransferServiceIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _connectionFactory = new TestConnectionFactory(_fixture.ConnectionString);
        _accountRepository = new AccountRepository(_connectionFactory);
        _transferRepository = new TransferRepository();
        _sut = new TransferService(
            _accountRepository,
            _transferRepository,
            _connectionFactory,
            NullLogger<TransferService>.Instance
        );
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ValidTransfer_UpdatesBalances_AndLogsTransfer()
    {
        var result = await _sut.ExecuteTransferAsync(
            Request(Account1001, Account1002, 100m, "T01")
        );

        Assert.True(result.Success);
        Assert.NotNull(result.TransferId);

        var from = await _accountRepository.GetByIdAsync(Account1001);
        var to = await _accountRepository.GetByIdAsync(Account1002);
        Assert.Equal(4900.00m, from!.Balance);
        Assert.Equal(1350.00m, to!.Balance);

        Assert.True(await TransferExistsAsync(result.TransferId!.Value));
    }

    [Fact]
    public async Task TransferUsingOverdraft_WithinLimit_Succeeds()
    {
        var result = await _sut.ExecuteTransferAsync(
            Request(Account2001, Account1001, 300m, "T02")
        );

        Assert.True(result.Success);
        var from = await _accountRepository.GetByIdAsync(Account2001);
        Assert.Equal(-150.00m, from!.Balance);
    }

    [Fact]
    public async Task InsufficientFunds_FailsAndLeavesBalancesUnchanged()
    {
        var beforeFrom = (await _accountRepository.GetByIdAsync(Account1002))!.Balance;
        var beforeTo = (await _accountRepository.GetByIdAsync(Account1001))!.Balance;

        var result = await _sut.ExecuteTransferAsync(
            Request(Account1002, Account1001, 1300m, "T03")
        );

        Assert.False(result.Success);
        Assert.Contains("Insufficient", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var afterFrom = (await _accountRepository.GetByIdAsync(Account1002))!.Balance;
        var afterTo = (await _accountRepository.GetByIdAsync(Account1001))!.Balance;
        Assert.Equal(beforeFrom, afterFrom);
        Assert.Equal(beforeTo, afterTo);
    }

    /// <summary>
    /// Runs several A->B and B->A transfers in parallel. With per-request lock ordering
    /// driven only by the request payload, SQL Server is expected to detect a deadlock
    /// cycle on at least one pair and kill the victim transaction (error 1205), which the
    /// service maps to <see cref="TransferStatus.ServerError"/>. With deterministic
    /// (lower-Guid first) lock ordering, all transfers serialize cleanly and the test
    /// passes.
    /// </summary>
    [Fact]
    public async Task ConcurrentOpposingTransfers_DoNotDeadlock()
    {
        const int pairs = 10;
        var tasks = new List<Task<TransferResult>>();

        for (int i = 0; i < pairs; i++)
        {
            tasks.Add(
                _sut.ExecuteTransferAsync(Request(Account1001, Account1002, 1m, $"A->B-{i}"))
            );
            tasks.Add(
                _sut.ExecuteTransferAsync(Request(Account1002, Account1001, 1m, $"B->A-{i}"))
            );
        }

        var results = await Task.WhenAll(tasks);

        var failures = results.Where(r => !r.Success).ToList();
        Assert.True(
            failures.Count == 0,
            $"{failures.Count}/{results.Length} transfers failed. First: status={failures.FirstOrDefault()?.Status} message={failures.FirstOrDefault()?.ErrorMessage}"
        );

        // Equal numbers of A->B and B->A transfers at the same amount must net to zero.
        var a = await _accountRepository.GetByIdAsync(Account1001);
        var b = await _accountRepository.GetByIdAsync(Account1002);
        Assert.Equal(5000m, a!.Balance);
        Assert.Equal(1250m, b!.Balance);
    }

    [Fact]
    public async Task RepeatedRequestWithSameIdempotencyKey_ReturnsOriginalTransferAndAppliesEffectsOnce()
    {
        const string key = "request-abc-123";

        var first = await _sut.ExecuteTransferAsync(
            Request(Account1001, Account1002, 100m, "T-IDEMP"),
            idempotencyKey: key
        );
        Assert.True(first.Success);

        var balanceAfterFirst = (await _accountRepository.GetByIdAsync(Account1001))!.Balance;

        var second = await _sut.ExecuteTransferAsync(
            Request(Account1001, Account1002, 100m, "T-IDEMP"),
            idempotencyKey: key
        );
        Assert.True(second.Success);
        Assert.Equal(first.TransferId, second.TransferId);

        var balanceAfterSecond = (await _accountRepository.GetByIdAsync(Account1001))!.Balance;
        Assert.Equal(balanceAfterFirst, balanceAfterSecond);

        Assert.Equal(1, await CountTransfersWithReferenceAsync("T-IDEMP"));
    }

    [Fact]
    public async Task UnknownSourceAccount_ReturnsNotFound()
    {
        var unknown = Guid.NewGuid();

        var result = await _sut.ExecuteTransferAsync(Request(unknown, Account1001, 50m, "T07"));

        Assert.False(result.Success);
        Assert.Equal(TransferStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task InactiveSenderAccount_FailsAndLeavesBalancesUnchanged()
    {
        var beforeSender = (await _accountRepository.GetByIdAsync(Account9001))!.Balance;
        var beforeReceiver = (await _accountRepository.GetByIdAsync(Account1001))!.Balance;

        var result = await _sut.ExecuteTransferAsync(Request(Account9001, Account1001, 50m, "T09"));

        Assert.False(result.Success);
        Assert.Contains("not active", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var afterSender = (await _accountRepository.GetByIdAsync(Account9001))!.Balance;
        var afterReceiver = (await _accountRepository.GetByIdAsync(Account1001))!.Balance;
        Assert.Equal(beforeSender, afterSender);
        Assert.Equal(beforeReceiver, afterReceiver);
    }

    /// <summary>
    /// Maps to T12 in the assignment test table: a failure that occurs after balances
    /// have been touched within the transaction must trigger a full rollback.
    /// </summary>
    [Fact]
    public async Task MidTransactionFailure_RollsBackBalancesAndDoesNotInsertTransfer()
    {
        var beforeFrom = (await _accountRepository.GetByIdAsync(Account1001))!.Balance;
        var beforeTo = (await _accountRepository.GetByIdAsync(Account1002))!.Balance;

        var faultyService = new TransferService(
            _accountRepository,
            new ThrowingTransferRepository(),
            _connectionFactory,
            NullLogger<TransferService>.Instance
        );

        var result = await faultyService.ExecuteTransferAsync(
            Request(Account1001, Account1002, 100m, "T12")
        );

        Assert.False(result.Success);
        Assert.Equal(TransferStatus.ServerError, result.Status);

        var afterFrom = (await _accountRepository.GetByIdAsync(Account1001))!.Balance;
        var afterTo = (await _accountRepository.GetByIdAsync(Account1002))!.Balance;
        Assert.Equal(beforeFrom, afterFrom);
        Assert.Equal(beforeTo, afterTo);

        Assert.Equal(0, await CountTransfersWithReferenceAsync("T12"));
    }

    private static TransferRequest Request(Guid from, Guid to, decimal amount, string reference) =>
        new TransferRequest
        {
            FromAccountId = from,
            ToAccountId = to,
            Amount = amount,
            Reference = reference,
            Description = "Integration test transfer",
        };

    private async Task<bool> TransferExistsAsync(Guid id)
    {
        await using var connection = (SqlConnection)_connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM Transfers WHERE Id = @Id",
            connection
        );
        command.Parameters.AddWithValue("@Id", id);
        var count = (int)(await command.ExecuteScalarAsync())!;
        return count == 1;
    }

    private async Task<int> CountTransfersWithReferenceAsync(string reference)
    {
        await using var connection = (SqlConnection)_connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM Transfers WHERE Reference = @Reference",
            connection
        );
        command.Parameters.AddWithValue("@Reference", reference);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// Substitute repository that throws after the service has already updated both balances,
    /// simulating a database failure mid-transaction.
    /// </summary>
    private sealed class ThrowingTransferRepository : ITransferRepository
    {
        public Task InsertAsync(
            Transfer transfer,
            DbConnection connection,
            DbTransaction transaction
        )
        {
            throw CreateSqlException();
        }

        public Task<Guid?> FindIdByIdempotencyKeyAsync(
            string idempotencyKey,
            DbConnection connection,
            DbTransaction transaction
        ) => Task.FromResult<Guid?>(null);

        private static SqlException CreateSqlException() =>
            (SqlException)
                System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
                    typeof(SqlException)
                );
    }
}
