using System.Data;
using BankTransferService.Interfaces;
using BankTransferService.Models.Domain;
using Microsoft.Data.SqlClient;
using SqlDbType = System.Data.SqlDbType;

namespace BankTransferService.Data;

/// <inheritdoc />
public class TransferRepository : ITransferRepository
{
    public async Task<TransferResult> ExecuteTransferAsync(
        Transfer transfer,
        IDbConnectionFactory connectionFactory
    )
    {
        var id = Guid.NewGuid();
        var createdUtc = DateTime.UtcNow;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);

        try
        {
            var fromAccount = await GetAccountForUpdateAsync(
                connection,
                transaction,
                transfer.FromAccountId
            );
            if (fromAccount is null)
                return TransferResult.NotFound(
                    $"Source account '{transfer.FromAccountId}' was not found."
                );

            var toAccount = await GetAccountForUpdateAsync(
                connection,
                transaction,
                transfer.ToAccountId
            );
            if (toAccount is null)
                return TransferResult.NotFound(
                    $"Destination account '{transfer.ToAccountId}' was not found."
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
            if (transfer.Amount > availableBalance)
                return TransferResult.Fail(
                    $"Insufficient funds. Available balance including overdraft: {availableBalance:F2}."
                );

            await UpdateBalanceAsync(
                connection,
                transaction,
                transfer.FromAccountId,
                -transfer.Amount
            );
            await UpdateBalanceAsync(
                connection,
                transaction,
                transfer.ToAccountId,
                +transfer.Amount
            );
            await InsertTransferLogAsync(connection, transaction, transfer, id, createdUtc);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return TransferResult.Ok(id);
    }

    private static async Task<Account?> GetAccountForUpdateAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid accountId
    )
    {
        const string sql = """
            SELECT Id, AccountNumber, OwnerName, Balance, OverdraftLimit, IsActive
            FROM Accounts WITH (UPDLOCK, HOLDLOCK)
            WHERE Id = @Id
            """;

        await using var cmd = new SqlCommand(sql, connection, transaction);
        cmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = accountId;

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new Account
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            AccountNumber = reader.GetString(reader.GetOrdinal("AccountNumber")),
            OwnerName = reader.GetString(reader.GetOrdinal("OwnerName")),
            Balance = reader.GetDecimal(reader.GetOrdinal("Balance")),
            OverdraftLimit = reader.GetDecimal(reader.GetOrdinal("OverdraftLimit")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
        };
    }

    private static async Task UpdateBalanceAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid accountId,
        decimal delta
    )
    {
        const string sql = "UPDATE Accounts SET Balance = Balance + @Delta WHERE Id = @Id";

        await using var cmd = new SqlCommand(sql, connection, transaction);
        cmd.Parameters.Add("@Delta", SqlDbType.Decimal).Value = delta;
        cmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = accountId;

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        if (rowsAffected != 1)
            throw new InvalidOperationException(
                $"Expected 1 row affected when updating account '{accountId}', but got {rowsAffected}."
            );
    }

    private static async Task InsertTransferLogAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Transfer transfer,
        Guid id,
        DateTime createdUtc
    )
    {
        const string sql = """
            INSERT INTO Transfers (Id, FromAccountId, ToAccountId, Amount, Reference, Description, CreatedUtc)
            VALUES (@Id, @FromAccountId, @ToAccountId, @Amount, @Reference, @Description, @CreatedUtc)
            """;

        await using var cmd = new SqlCommand(sql, connection, transaction);

        cmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        cmd.Parameters.Add("@FromAccountId", SqlDbType.UniqueIdentifier).Value =
            transfer.FromAccountId;
        cmd.Parameters.Add("@ToAccountId", SqlDbType.UniqueIdentifier).Value = transfer.ToAccountId;
        cmd.Parameters.Add("@Amount", SqlDbType.Decimal).Value = transfer.Amount;
        cmd.Parameters.Add("@Reference", SqlDbType.NVarChar, 140).Value = transfer.Reference;
        cmd.Parameters.Add("@Description", SqlDbType.NVarChar, 300).Value = transfer.Description
            is null
            ? DBNull.Value
            : transfer.Description;
        cmd.Parameters.Add("@CreatedUtc", SqlDbType.DateTime2).Value = createdUtc;

        await cmd.ExecuteNonQueryAsync();
    }
}
