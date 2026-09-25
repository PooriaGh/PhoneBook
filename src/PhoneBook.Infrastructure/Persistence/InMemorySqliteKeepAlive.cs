using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace PhoneBook.Infrastructure.Persistence;

/// <summary>
/// Holds one connection open for the lifetime of the process. A shared-cache in-memory SQLite database
/// is destroyed when its last connection closes; this keeps it alive (research R-02).
/// </summary>
public sealed class InMemorySqliteKeepAlive : IDisposable
{
    private readonly SqliteConnection _connection;

    public InMemorySqliteKeepAlive(IOptions<DatabaseOptions> options)
    {
        _connection = new SqliteConnection(options.Value.ConnectionString);
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();
}
