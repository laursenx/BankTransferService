using System.Data.Common;

namespace BankTransferService.Interfaces;

/// <summary>
/// Factory for creating database connections. Returns the abstract <see cref="DbConnection"/>
/// so consumers stay provider-agnostic and can be unit-tested without a real database.
/// </summary>
public interface IDbConnectionFactory
{
    DbConnection CreateConnection();
}
