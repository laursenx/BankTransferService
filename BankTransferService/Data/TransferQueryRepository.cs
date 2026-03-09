using BankTransferService.Interfaces;
using BankTransferService.Models;
using Microsoft.Data.SqlClient;

namespace BankTransferService.Data;

/// <inheritdoc />
public class TransferQueryRepository : ITransferQueryRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TransferQueryRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<Transfer>> GetByAccountIdAsync(Guid accountId)
    {
        const string sql = """
            SELECT Id, FromAccountId, ToAccountId, Amount, Reference, Description, CreatedUtc
            FROM Transfers
            WHERE FromAccountId = @AccountId OR ToAccountId = @AccountId
            ORDER BY CreatedUtc DESC
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@AccountId", System.Data.SqlDbType.UniqueIdentifier).Value =
            accountId;

        var transfers = new List<Transfer>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            transfers.Add(MapTransfer(reader));

        return transfers;
    }

    private static Transfer MapTransfer(SqlDataReader reader)
    {
        var id = reader.GetOrdinal("Id");
        var fromAccountId = reader.GetOrdinal("FromAccountId");
        var toAccountId = reader.GetOrdinal("ToAccountId");
        var amount = reader.GetOrdinal("Amount");
        var reference = reader.GetOrdinal("Reference");
        var description = reader.GetOrdinal("Description");
        var createdUtc = reader.GetOrdinal("CreatedUtc");

        return new Transfer
        {
            Id = reader.GetGuid(id),
            FromAccountId = reader.GetGuid(fromAccountId),
            ToAccountId = reader.GetGuid(toAccountId),
            Amount = reader.GetDecimal(amount),
            Reference = reader.GetString(reference),
            Description = reader.IsDBNull(description) ? null : reader.GetString(description),
            CreatedUtc = reader.GetDateTime(createdUtc),
        };
    }
}
