using BankTransferService.Interfaces;
using BankTransferService.Models.Domain;
using Microsoft.Data.SqlClient;
using SqlDbType = System.Data.SqlDbType;

namespace BankTransferService.Data;

/// <inheritdoc />
public class TransferRepository : ITransferRepository
{
    public async Task<Guid> ExecuteTransferAsync(Transfer transfer, IDbConnectionFactory connectionFactory)
    {
        var id = Guid.NewGuid();
        var createdUtc = DateTime.UtcNow;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            await UpdateBalanceAsync(connection, transaction, transfer.FromAccountId, -transfer.Amount);
            await UpdateBalanceAsync(connection, transaction, transfer.ToAccountId, +transfer.Amount);
            await InsertTransferLogAsync(connection, transaction, transfer, id, createdUtc);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return id;
    }

    private static async Task UpdateBalanceAsync(
        SqlConnection connection, SqlTransaction transaction, Guid accountId, decimal delta)
    {
        const string sql = "UPDATE Accounts SET Balance = Balance + @Delta WHERE Id = @Id";

        await using var cmd = new SqlCommand(sql, connection, transaction);
        cmd.Parameters.Add("@Delta", SqlDbType.Decimal).Value = delta;
        cmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = accountId;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertTransferLogAsync(
        SqlConnection connection, SqlTransaction transaction,
        Transfer transfer, Guid id, DateTime createdUtc)
    {
        const string sql = """
            INSERT INTO Transfers (Id, FromAccountId, ToAccountId, Amount, Reference, Description, CreatedUtc)
            VALUES (@Id, @FromAccountId, @ToAccountId, @Amount, @Reference, @Description, @CreatedUtc)
            """;

        await using var cmd = new SqlCommand(sql, connection, transaction);

        cmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        cmd.Parameters.Add("@FromAccountId", SqlDbType.UniqueIdentifier).Value = transfer.FromAccountId;
        cmd.Parameters.Add("@ToAccountId", SqlDbType.UniqueIdentifier).Value = transfer.ToAccountId;
        cmd.Parameters.Add("@Amount", SqlDbType.Decimal).Value = transfer.Amount;
        cmd.Parameters.Add("@Reference", SqlDbType.NVarChar, 140).Value = transfer.Reference;
        cmd.Parameters.Add("@Description", SqlDbType.NVarChar, 300).Value = transfer.Description is null ? DBNull.Value : transfer.Description;
        cmd.Parameters.Add("@CreatedUtc", SqlDbType.DateTime2).Value = createdUtc;

        await cmd.ExecuteNonQueryAsync();
    }
}
