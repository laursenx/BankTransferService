using System.Data;
using System.Data.Common;
using BankTransferService.Interfaces;
using BankTransferService.Models.Domain;
using Microsoft.Data.SqlClient;

namespace BankTransferService.Data;

/// <inheritdoc />
public class AccountRepository : IAccountRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AccountRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Account?> GetByIdAsync(Guid id)
    {
        const string sql = """
            SELECT Id, AccountNumber, OwnerName, Balance, OverdraftLimit, IsActive
            FROM Accounts
            WHERE Id = @Id
            """;

        await using var connection = (SqlConnection)_connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return MapAccount(reader);
    }

    public async Task<Account?> GetByIdForUpdateAsync(
        Guid id,
        DbConnection connection,
        DbTransaction transaction
    )
    {
        const string sql = """
            SELECT Id, AccountNumber, OwnerName, Balance, OverdraftLimit, IsActive
            FROM Accounts WITH (UPDLOCK, HOLDLOCK)
            WHERE Id = @Id
            """;

        await using var command = new SqlCommand(
            sql,
            (SqlConnection)connection,
            (SqlTransaction)transaction
        );
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return MapAccount(reader);
    }

    public async Task UpdateBalanceAsync(
        Guid id,
        decimal delta,
        DbConnection connection,
        DbTransaction transaction
    )
    {
        const string sql = "UPDATE Accounts SET Balance = Balance + @Delta WHERE Id = @Id";

        await using var command = new SqlCommand(
            sql,
            (SqlConnection)connection,
            (SqlTransaction)transaction
        );
        command.Parameters.Add("@Delta", SqlDbType.Decimal).Value = delta;
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;

        var rowsAffected = await command.ExecuteNonQueryAsync();
        if (rowsAffected != 1)
            throw new InvalidOperationException(
                $"Expected 1 row affected when updating account '{id}', but got {rowsAffected}."
            );
    }

    private static Account MapAccount(SqlDataReader reader)
    {
        var id = reader.GetOrdinal("Id");
        var accountNumber = reader.GetOrdinal("AccountNumber");
        var ownerName = reader.GetOrdinal("OwnerName");
        var balance = reader.GetOrdinal("Balance");
        var overdraftLimit = reader.GetOrdinal("OverdraftLimit");
        var isActive = reader.GetOrdinal("IsActive");

        return new Account
        {
            Id = reader.GetGuid(id),
            AccountNumber = reader.GetString(accountNumber),
            OwnerName = reader.GetString(ownerName),
            Balance = reader.GetDecimal(balance),
            OverdraftLimit = reader.GetDecimal(overdraftLimit),
            IsActive = reader.GetBoolean(isActive),
        };
    }
}
