using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Npgsql;
using PhoneBook.Application.Abstractions.Data;

namespace PhoneBook.Infrastructure.Persistence;

internal sealed class SqlConnectionFactory(IOptions<DatabaseOptions> options) : ISqlConnectionFactory
{
    public DbConnection CreateConnection() => options.Value.Provider switch
    {
        DatabaseProvider.Postgres => new NpgsqlConnection(options.Value.ConnectionString),
        _ => new SqliteConnection(options.Value.ConnectionString),
    };
}
