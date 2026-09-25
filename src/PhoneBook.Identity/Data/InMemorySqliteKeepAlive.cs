using Microsoft.Data.Sqlite;

namespace PhoneBook.Identity.Data;

/// <summary>
/// Keeps the shared in-memory SQLite database alive for the process lifetime. A separate copy of the API's
/// helper, because the Identity host deliberately has no project references.
/// </summary>
public sealed class InMemorySqliteKeepAlive : IDisposable
{
    private readonly SqliteConnection _connection;

    public InMemorySqliteKeepAlive(IConfiguration configuration)
    {
        _connection = new SqliteConnection(configuration.GetConnectionString(IdentitySettings.ConnectionStringName));
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();
}
