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
    private readonly ITransferRepository _transferRepo;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly TransferService _sut; // System Under Test

    private static readonly Guid FromId = Guid.NewGuid();
    private static readonly Guid ToId = Guid.NewGuid();

    public TransferServiceTests()
    {
        _transferRepo = Substitute.For<ITransferRepository>();
        _connectionFactory = Substitute.For<IDbConnectionFactory>();
        _sut = new TransferService(
            _transferRepo,
            _connectionFactory,
            NullLogger<TransferService>.Instance
        );
    }

    // Amount validation (handled by the service before hitting the repository)
    [Fact]
    public async Task ExecuteTransfer_ZeroAmount_ReturnsFail()
    {
        var result = await _sut.ExecuteTransferAsync(ValidRequest(FromId, ToId, 0m));
        Assert.False(result.Success);
        Assert.Contains("greater than 0", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteTransfer_NegativeAmount_ReturnsFail()
    {
        var result = await _sut.ExecuteTransferAsync(ValidRequest(FromId, ToId, -10m));
        Assert.False(result.Success);
        Assert.Contains("greater than 0", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // Same account (handled by the service before hitting the repository)
    [Fact]
    public async Task ExecuteTransfer_SameAccount_ReturnsFail()
    {
        var result = await _sut.ExecuteTransferAsync(ValidRequest(FromId, FromId, 50m));
        Assert.False(result.Success);
        Assert.Contains("different", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // Account not found (returned by the repository via pessimistic locking flow)
    [Fact]
    public async Task ExecuteTransfer_FromAccountNotFound_ReturnsNotFound()
    {
        _transferRepo
            .ExecuteTransferAsync(Arg.Any<Transfer>(), Arg.Any<IDbConnectionFactory>())
            .Returns(TransferResult.NotFound($"Source account '{FromId}' was not found."));

        var result = await _sut.ExecuteTransferAsync(ValidRequest(FromId, ToId, 100m));

        Assert.False(result.Success);
        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task ExecuteTransfer_ToAccountNotFound_ReturnsNotFound()
    {
        _transferRepo
            .ExecuteTransferAsync(Arg.Any<Transfer>(), Arg.Any<IDbConnectionFactory>())
            .Returns(TransferResult.NotFound($"Destination account '{ToId}' was not found."));

        var result = await _sut.ExecuteTransferAsync(ValidRequest(FromId, ToId, 100m));

        Assert.False(result.Success);
        Assert.True(result.IsNotFound);
    }

    // Inactive accounts (returned by the repository via pessimistic locking flow)
    [Fact]
    public async Task ExecuteTransfer_InactiveFromAccount_ReturnsFail()
    {
        _transferRepo
            .ExecuteTransferAsync(Arg.Any<Transfer>(), Arg.Any<IDbConnectionFactory>())
            .Returns(TransferResult.Fail("Source account '9001' is not active."));

        var result = await _sut.ExecuteTransferAsync(ValidRequest(FromId, ToId, 50m));

        Assert.False(result.Success);
        Assert.Contains("not active", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteTransfer_InactiveToAccount_ReturnsFail()
    {
        _transferRepo
            .ExecuteTransferAsync(Arg.Any<Transfer>(), Arg.Any<IDbConnectionFactory>())
            .Returns(TransferResult.Fail("Destination account '9001' is not active."));

        var result = await _sut.ExecuteTransferAsync(ValidRequest(FromId, ToId, 50m));

        Assert.False(result.Success);
        Assert.Contains("not active", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // Insufficient funds (returned by the repository via pessimistic locking flow)
    [Fact]
    public async Task ExecuteTransfer_InsufficientFunds_ReturnsFail()
    {
        _transferRepo
            .ExecuteTransferAsync(Arg.Any<Transfer>(), Arg.Any<IDbConnectionFactory>())
            .Returns(
                TransferResult.Fail(
                    "Insufficient funds. Available balance including overdraft: 5000.00."
                )
            );

        var result = await _sut.ExecuteTransferAsync(ValidRequest(FromId, ToId, 5001m));

        Assert.False(result.Success);
        Assert.Contains("Insufficient", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteTransfer_ExceedsOverdraftLimit_ReturnsFail()
    {
        _transferRepo
            .ExecuteTransferAsync(Arg.Any<Transfer>(), Arg.Any<IDbConnectionFactory>())
            .Returns(
                TransferResult.Fail(
                    "Insufficient funds. Available balance including overdraft: 350.00."
                )
            );

        var result = await _sut.ExecuteTransferAsync(ValidRequest(FromId, ToId, 351m));

        Assert.False(result.Success);
        Assert.Contains("Insufficient", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteTransfer_WithinOverdraftLimit_ReturnsSuccess()
    {
        var expectedId = Guid.NewGuid();
        _transferRepo
            .ExecuteTransferAsync(Arg.Any<Transfer>(), Arg.Any<IDbConnectionFactory>())
            .Returns(TransferResult.Ok(expectedId));

        var result = await _sut.ExecuteTransferAsync(ValidRequest(FromId, ToId, 300m));

        Assert.True(result.Success);
        Assert.Equal(expectedId, result.TransferId);
    }

    // Database error handling
    [Fact]
    public async Task ExecuteTransfer_DatabaseError_ReturnsServerError()
    {
        _transferRepo
            .ExecuteTransferAsync(Arg.Any<Transfer>(), Arg.Any<IDbConnectionFactory>())
            .Returns<TransferResult>(_ => throw CreateSqlException());

        var result = await _sut.ExecuteTransferAsync(ValidRequest(FromId, ToId, 100m));

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
