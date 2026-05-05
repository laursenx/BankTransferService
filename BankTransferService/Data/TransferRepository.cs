using System.Data;
using System.Data.Common;
using BankTransferService.Interfaces;
using BankTransferService.Models.Domain;
using Microsoft.Data.SqlClient;

namespace BankTransferService.Data;

/// <inheritdoc />
public class TransferRepository : ITransferRepository
{
    public async Task InsertAsync(
        Transfer transfer,
        DbConnection connection,
        DbTransaction transaction
    )
    {
        const string sql = """
            INSERT INTO Transfers (Id, FromAccountId, ToAccountId, Amount, Reference, Description, IdempotencyKey, CreatedUtc)
            VALUES (@Id, @FromAccountId, @ToAccountId, @Amount, @Reference, @Description, @IdempotencyKey, @CreatedUtc)
            """;

        await using var command = new SqlCommand(
            sql,
            (SqlConnection)connection,
            (SqlTransaction)transaction
        );

        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = transfer.Id;
        command.Parameters.Add("@FromAccountId", SqlDbType.UniqueIdentifier).Value =
            transfer.FromAccountId;
        command.Parameters.Add("@ToAccountId", SqlDbType.UniqueIdentifier).Value =
            transfer.ToAccountId;
        command.Parameters.Add("@Amount", SqlDbType.Decimal).Value = transfer.Amount;
        command.Parameters.Add("@Reference", SqlDbType.NVarChar, 140).Value = transfer.Reference;
        command.Parameters.Add("@Description", SqlDbType.NVarChar, 300).Value =
            transfer.Description is null ? DBNull.Value : transfer.Description;
        command.Parameters.Add("@IdempotencyKey", SqlDbType.NVarChar, 64).Value =
            transfer.IdempotencyKey is null ? DBNull.Value : transfer.IdempotencyKey;
        command.Parameters.Add("@CreatedUtc", SqlDbType.DateTime2).Value = transfer.CreatedUtc;

        var rowsAffected = await command.ExecuteNonQueryAsync();
        if (rowsAffected != 1)
            throw new InvalidOperationException(
                $"Expected 1 row affected when inserting transfer '{transfer.Id}', but got {rowsAffected}."
            );
    }

    public async Task<Guid?> FindIdByIdempotencyKeyAsync(
        string idempotencyKey,
        DbConnection connection,
        DbTransaction transaction
    )
    {
        const string sql = "SELECT Id FROM Transfers WHERE IdempotencyKey = @IdempotencyKey";

        await using var command = new SqlCommand(
            sql,
            (SqlConnection)connection,
            (SqlTransaction)transaction
        );
        command.Parameters.Add("@IdempotencyKey", SqlDbType.NVarChar, 64).Value = idempotencyKey;

        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? null : (Guid)result;
    }
}
