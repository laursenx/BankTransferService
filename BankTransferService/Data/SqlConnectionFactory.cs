using BankTransferService.Interfaces;
using Microsoft.Data.SqlClient;

namespace BankTransferService.Data;

/// <inheritdoc />
public class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("BankDb")
            ?? throw new InvalidOperationException("Connection string 'BankDb' is not configured.");
    }

    public SqlConnection CreateConnection() => new SqlConnection(_connectionString);
}
