using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace BankTransferService.IntegrationTests;

/// <summary>
/// xUnit class fixture that owns a SQL Server container for the lifetime of a test class.
/// Containers are slow to start (~5s on cached images, ~90s on first pull), so the fixture
/// is shared across tests in the same class. Individual tests call <see cref="ResetAsync"/>
/// to drop and recreate the schema and seed data — that gives full isolation without paying
/// the container startup cost per test.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private static readonly string SchemaScriptPath = Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "BankTransferService",
        "database",
        "bank-transfer-service-schema-seed.sql"
    );

    private readonly MsSqlContainer _container = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2022-latest"
    ).Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await ResetAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>
    /// Re-runs the production schema-seed script so each test starts from a known state.
    /// </summary>
    public async Task ResetAsync()
    {
        var script = await File.ReadAllTextAsync(SchemaScriptPath);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        foreach (var batch in SplitBatches(script))
        {
            await using var command = new SqlCommand(batch, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// SQL Server scripts use the GO separator (a sqlcmd directive, not real T-SQL).
    /// We split on GO so each batch can be sent to the server as its own command.
    /// </summary>
    private static IEnumerable<string> SplitBatches(string script)
    {
        var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline);
        return batches.Select(b => b.Trim()).Where(b => b.Length > 0);
    }
}
