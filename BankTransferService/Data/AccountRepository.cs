using BankTransferService.Interfaces;
using BankTransferService.Models;
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

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", System.Data.SqlDbType.UniqueIdentifier).Value = id;

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return MapAccount(reader);
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
