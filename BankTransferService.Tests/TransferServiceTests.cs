using System.Data.Common;
using System.Globalization;
using BankTransferService.Interfaces;
using BankTransferService.Models.Domain;
using BankTransferService.Models.Requests;
using BankTransferService.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BankTransferService.Tests;

/// <summary>
/// Unit tests for TransferService.
///
/// The tests are split in two groups:
/// - Pre-DB validation tests exercise the service entry point and assert that
///   pure-input rules short-circuit before any connection is opened.
/// - Pure business rule tests call <see cref="TransferService.ValidateBusinessRules"/>
///   directly with constructed account state, so the rules are testable without
///   any mocking of database primitives.
///
/// Orchestration that touches a real connection (locks, transaction commit/rollback)
/// is covered by integration tests, not by mocks.
/// </summary>
public class TransferServiceTests
{
    private readonly IAccountRepository _accountRepo;
    private readonly ITransferRepository _transferRepo;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly TransferService _sut;

    private static readonly Guid FromId = Guid.NewGuid();
    private static readonly Guid ToId = Guid.NewGuid();

    public TransferServiceTests()
    {
        _accountRepo = Substitute.For<IAccountRepository>();
        _transferRepo = Substitute.For<ITransferRepository>();
        _connectionFactory = Substitute.For<IDbConnectionFactory>();
        _sut = new TransferService(
            _accountRepo,
            _transferRepo,
            _connectionFactory,
            NullLogger<TransferService>.Instance
        );
    }

    // ---------------------------------------------------------------------
    // Pre-DB validation - short-circuits before any connection is opened
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExecuteTransfer_ZeroAmount_ReturnsFail()
    {
        var result = await _sut.ExecuteTransferAsync(ValidRequest(FromId, ToId, 0m));

        Assert.False(result.Success);
        Assert.Contains("greater than 0", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _connectionFactory.DidNotReceive().CreateConnection();
    }

    [Fact]
    public async Task ExecuteTransfer_NegativeAmount_ReturnsFail()
    {
        var result = await _sut.ExecuteTransferAsync(ValidRequest(FromId, ToId, -10m));

        Assert.False(result.Success);
        Assert.Contains("greater than 0", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _connectionFactory.DidNotReceive().CreateConnection();
    }

    [Fact]
    public async Task ExecuteTransfer_SameAccount_ReturnsFail()
    {
        var result = await _sut.ExecuteTransferAsync(ValidRequest(FromId, FromId, 50m));

        Assert.False(result.Success);
        Assert.Contains("different", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _connectionFactory.DidNotReceive().CreateConnection();
    }

    // ---------------------------------------------------------------------
    // Database error path - factory throws DbException, service maps to 500
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExecuteTransfer_DatabaseError_ReturnsServerError()
    {
        _connectionFactory
            .CreateConnection()
            .Returns<DbConnection>(_ => throw CreateSqlException());

        var result = await _sut.ExecuteTransferAsync(ValidRequest(FromId, ToId, 100m));

        Assert.False(result.Success);
        Assert.Equal(TransferStatus.ServerError, result.Status);
    }

    [Fact]
    public async Task ExecuteTransfer_InvalidOperation_ReturnsServerError()
    {
        // ADO.NET surfaces a number of failure modes (e.g. connection in wrong state,
        // transaction already committed) as InvalidOperationException rather than
        // DbException. The service must map those to a clean ServerError response
        // instead of letting them bubble out as an unhandled 500.
        _connectionFactory
            .CreateConnection()
            .Returns<DbConnection>(_ => throw new InvalidOperationException("connection state"));

        var result = await _sut.ExecuteTransferAsync(ValidRequest(FromId, ToId, 100m));

        Assert.False(result.Success);
        Assert.Equal(TransferStatus.ServerError, result.Status);
    }

    // ---------------------------------------------------------------------
    // Pure business rule tests - call the static validator directly
    // ---------------------------------------------------------------------

    [Fact]
    public void ValidateBusinessRules_InactiveFromAccount_ReturnsFail()
    {
        var from = Account(balance: 1000, overdraft: 0, isActive: false, number: "9001");
        var to = Account(balance: 1000, overdraft: 0, isActive: true, number: "1001");

        var result = TransferService.ValidateBusinessRules(from, to, 50m);

        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Contains("not active", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateBusinessRules_InactiveToAccount_ReturnsFail()
    {
        var from = Account(balance: 1000, overdraft: 0, isActive: true, number: "1001");
        var to = Account(balance: 1000, overdraft: 0, isActive: false, number: "9001");

        var result = TransferService.ValidateBusinessRules(from, to, 50m);

        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Contains("not active", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateBusinessRules_InsufficientFunds_ReturnsFail()
    {
        var from = Account(balance: 100, overdraft: 0, isActive: true);
        var to = Account(balance: 0, overdraft: 0, isActive: true);

        var result = TransferService.ValidateBusinessRules(from, to, 200m);

        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Contains("Insufficient", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateBusinessRules_ExceedsOverdraftLimit_ReturnsFail()
    {
        var from = Account(balance: 150, overdraft: 200, isActive: true);
        var to = Account(balance: 0, overdraft: 0, isActive: true);

        var result = TransferService.ValidateBusinessRules(from, to, 351m);

        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Contains("Insufficient", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateBusinessRules_WithinOverdraftLimit_ReturnsNull()
    {
        var from = Account(balance: 150, overdraft: 200, isActive: true);
        var to = Account(balance: 0, overdraft: 0, isActive: true);

        var result = TransferService.ValidateBusinessRules(from, to, 300m);

        Assert.Null(result);
    }

    [Fact]
    public void ValidateBusinessRules_InsufficientFunds_FormatsBalanceWithInvariantCulture()
    {
        // Regression test: error messages crossing the API boundary must use a stable
        // decimal separator regardless of the server's regional settings. Without this
        // a Danish-locale server would emit "1400,00" while an invariant-locale server
        // would emit "1400.00", forcing clients to parse both.
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("da-DK");

            var from = Account(balance: 1000m, overdraft: 400m, isActive: true);
            var to = Account(balance: 0m, overdraft: 0m, isActive: true);

            var result = TransferService.ValidateBusinessRules(from, to, 9999m);

            Assert.NotNull(result);
            Assert.False(result!.Success);
            Assert.Contains("1400.00", result.ErrorMessage);
            Assert.DoesNotContain("1400,00", result.ErrorMessage);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ValidateBusinessRules_AmountExactlyEqualToAvailable_ReturnsNull()
    {
        var from = Account(balance: 100, overdraft: 50, isActive: true);
        var to = Account(balance: 0, overdraft: 0, isActive: true);

        var result = TransferService.ValidateBusinessRules(from, to, 150m);

        Assert.Null(result);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static TransferRequest ValidRequest(Guid from, Guid to, decimal amount) =>
        new TransferRequest
        {
            FromAccountId = from,
            ToAccountId = to,
            Amount = amount,
            Reference = "TEST-001",
            Description = "Unit test transfer",
        };

    private static Account Account(
        decimal balance,
        decimal overdraft,
        bool isActive,
        string number = "1000"
    ) =>
        new Account
        {
            Id = Guid.NewGuid(),
            AccountNumber = number,
            OwnerName = "Test Owner",
            Balance = balance,
            OverdraftLimit = overdraft,
            IsActive = isActive,
        };

    /// <summary>
    /// SqlException has no public constructor; create one via reflection for testing purposes.
    /// </summary>
    private static Microsoft.Data.SqlClient.SqlException CreateSqlException() =>
        (Microsoft.Data.SqlClient.SqlException)
            System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
                typeof(Microsoft.Data.SqlClient.SqlException)
            );
}
