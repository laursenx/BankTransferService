using Microsoft.Data.SqlClient;

namespace BankTransferService.Interfaces;

/// <summary>
/// Factory for creating SQL connections. Enables testability and DI.
/// </summary>
public interface IDbConnectionFactory
{
    SqlConnection CreateConnection();
}
