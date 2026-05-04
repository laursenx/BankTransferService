using System.Data.Common;
using BankTransferService.Interfaces;
using Microsoft.Data.SqlClient;

namespace BankTransferService.IntegrationTests;

/// <summary>
/// Test-only connection factory that hands out connections backed by the container.
/// The production factory reads from IConfiguration; this one takes the connection
/// string directly so tests don't need to spin up the full host.
/// </summary>
public sealed class TestConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public TestConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public DbConnection CreateConnection() => new SqlConnection(_connectionString);
}
