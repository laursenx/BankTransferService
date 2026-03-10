using BankTransferService.Interfaces;
using BankTransferService.Models.Domain;
using BankTransferService.Models.Requests;
using BankTransferService.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BankTransferService.Tests;

/// <summary>
/// Unit tests for TransferService business rules.
/// Repositories and connection factory are substituted so no database is needed.
/// </summary>
public class TransferServiceTests
{
    private readonly IAccountRepository _accountRepo;
    private readonly ITransferRepository _transferRepo;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly TransferService _sut; // System Under Test

    private static readonly Account ActiveFrom = new Account
    {
        Id = Guid.NewGuid(),
        AccountNumber = "1001",
        OwnerName = "Operating North",
        Balance = 5000.00m,
        OverdraftLimit = 0.00m,
        IsActive = true,
    };

    private static readonly Account ActiveTo = new Account
    {
        Id = Guid.NewGuid(),
        AccountNumber = "1002",
        OwnerName = "Operating South",
        Balance = 1250.00m,
        OverdraftLimit = 0.00m,
        IsActive = true,
    };

    private static readonly Account InactiveAccount = new Account
    {
        Id = Guid.NewGuid(),
        AccountNumber = "9001",
        OwnerName = "Dormant Account",
        Balance = 800.00m,
        OverdraftLimit = 0.00m,
        IsActive = false,
    };

    private static readonly Account OverdraftAccount = new Account
    {
        Id = Guid.NewGuid(),
        AccountNumber = "2001",
        OwnerName = "Private Buffer",
        Balance = 150.00m,
        OverdraftLimit = 200.00m,
        IsActive = true,
    };

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

    // Amount validation
    [Fact]
    public async Task ExecuteTransfer_ZeroAmount_ReturnsFail()
    {
        var result = await _sut.ExecuteTransferAsync(ValidRequest(ActiveFrom.Id, ActiveTo.Id, 0m));
        Assert.False(result.Success);
        Assert.Contains("greater than 0", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteTransfer_NegativeAmount_ReturnsFail()
    {
        var result = await _sut.ExecuteTransferAsync(
            ValidRequest(ActiveFrom.Id, ActiveTo.Id, -10m)
        );
        Assert.False(result.Success);
        Assert.Contains("greater than 0", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // Same account
    [Fact]
    public async Task ExecuteTransfer_SameAccount_ReturnsFail()
    {
        var result = await _sut.ExecuteTransferAsync(
            ValidRequest(ActiveFrom.Id, ActiveFrom.Id, 50m)
        );
        Assert.False(result.Success);
        Assert.Contains("different", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // Account not found
    [Fact]
    public async Task ExecuteTransfer_FromAccountNotFound_ReturnsNotFound()
    {
        _accountRepo.GetByIdAsync(ActiveFrom.Id).Returns(Task.FromResult<Account?>(null));
        _accountRepo.GetByIdAsync(ActiveTo.Id).Returns(Task.FromResult<Account?>(ActiveTo));

        var result = await _sut.ExecuteTransferAsync(
            ValidRequest(ActiveFrom.Id, ActiveTo.Id, 100m)
        );

        Assert.False(result.Success);
        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task ExecuteTransfer_ToAccountNotFound_ReturnsNotFound()
    {
        _accountRepo.GetByIdAsync(ActiveFrom.Id).Returns(Task.FromResult<Account?>(ActiveFrom));
        _accountRepo.GetByIdAsync(ActiveTo.Id).Returns(Task.FromResult<Account?>(null));

        var result = await _sut.ExecuteTransferAsync(
            ValidRequest(ActiveFrom.Id, ActiveTo.Id, 100m)
        );

        Assert.False(result.Success);
        Assert.True(result.IsNotFound);
    }

    // Inactive accounts
    [Fact]
    public async Task ExecuteTransfer_InactiveFromAccount_ReturnsFail()
    {
        _accountRepo
            .GetByIdAsync(InactiveAccount.Id)
            .Returns(Task.FromResult<Account?>(InactiveAccount));
        _accountRepo.GetByIdAsync(ActiveTo.Id).Returns(Task.FromResult<Account?>(ActiveTo));

        var result = await _sut.ExecuteTransferAsync(
            ValidRequest(InactiveAccount.Id, ActiveTo.Id, 50m)
        );

        Assert.False(result.Success);
        Assert.Contains("not active", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteTransfer_InactiveToAccount_ReturnsFail()
    {
        _accountRepo.GetByIdAsync(ActiveFrom.Id).Returns(Task.FromResult<Account?>(ActiveFrom));
        _accountRepo
            .GetByIdAsync(InactiveAccount.Id)
            .Returns(Task.FromResult<Account?>(InactiveAccount));

        var result = await _sut.ExecuteTransferAsync(
            ValidRequest(ActiveFrom.Id, InactiveAccount.Id, 50m)
        );

        Assert.False(result.Success);
        Assert.Contains("not active", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // Insufficient funds
    [Fact]
    public async Task ExecuteTransfer_InsufficientFunds_ReturnsFail()
    {
        _accountRepo.GetByIdAsync(ActiveFrom.Id).Returns(Task.FromResult<Account?>(ActiveFrom));
        _accountRepo.GetByIdAsync(ActiveTo.Id).Returns(Task.FromResult<Account?>(ActiveTo));

        // Balance = 5000, OverdraftLimit = 0 -- max transfer is 5000
        var result = await _sut.ExecuteTransferAsync(
            ValidRequest(ActiveFrom.Id, ActiveTo.Id, 5001m)
        );

        Assert.False(result.Success);
        Assert.Contains("Insufficient", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteTransfer_ExceedsOverdraftLimit_ReturnsFail()
    {
        _accountRepo
            .GetByIdAsync(OverdraftAccount.Id)
            .Returns(Task.FromResult<Account?>(OverdraftAccount));
        _accountRepo.GetByIdAsync(ActiveTo.Id).Returns(Task.FromResult<Account?>(ActiveTo));

        // Balance = 150, OverdraftLimit = 200 -- max transfer is 350; 351 exceeds it
        var result = await _sut.ExecuteTransferAsync(
            ValidRequest(OverdraftAccount.Id, ActiveTo.Id, 351m)
        );

        Assert.False(result.Success);
        Assert.Contains("Insufficient", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteTransfer_WithinOverdraftLimit_ReturnsSuccess()
    {
        var expectedId = Guid.NewGuid();
        _accountRepo
            .GetByIdAsync(OverdraftAccount.Id)
            .Returns(Task.FromResult<Account?>(OverdraftAccount));
        _accountRepo.GetByIdAsync(ActiveTo.Id).Returns(Task.FromResult<Account?>(ActiveTo));
        _transferRepo
            .ExecuteTransferAsync(Arg.Any<Transfer>(), Arg.Any<IDbConnectionFactory>())
            .Returns(Task.FromResult(expectedId));

        // Balance = 150, OverdraftLimit = 200 -- 150 - 300 = -150, within the -200 limit
        var result = await _sut.ExecuteTransferAsync(
            ValidRequest(OverdraftAccount.Id, ActiveTo.Id, 300m)
        );

        Assert.True(result.Success);
        Assert.Equal(expectedId, result.TransferId);
    }

    // Database error handling
    [Fact]
    public async Task ExecuteTransfer_DatabaseError_ReturnsServerError()
    {
        _accountRepo.GetByIdAsync(ActiveFrom.Id).Returns(Task.FromResult<Account?>(ActiveFrom));
        _accountRepo.GetByIdAsync(ActiveTo.Id).Returns(Task.FromResult<Account?>(ActiveTo));
        _transferRepo
            .ExecuteTransferAsync(Arg.Any<Transfer>(), Arg.Any<IDbConnectionFactory>())
            .Returns<Task<Guid>>(_ => Task.FromException<Guid>(CreateSqlException()));

        var result = await _sut.ExecuteTransferAsync(
            ValidRequest(ActiveFrom.Id, ActiveTo.Id, 100m)
        );

        Assert.False(result.Success);
        Assert.True(result.IsServerError);
    }

    // Helpers
    private static TransferRequest ValidRequest(Guid from, Guid to, decimal amount) =>
        new TransferRequest
        {
            FromAccountId = from,
            ToAccountId = to,
            Amount = amount,
            Reference = "TEST-001",
            Description = "Unit test transfer",
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
