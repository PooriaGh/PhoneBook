using System.ComponentModel.DataAnnotations;

namespace PhoneBook.Infrastructure.Persistence;

public enum DatabaseProvider
{
    /// <summary>In-memory SQLite (default): nothing is persisted, as the brief requires.</summary>
    Sqlite = 0,

    /// <summary>PostgreSQL: used by Testcontainers integration tests and the compose profile.</summary>
    Postgres = 1,
}

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;

    [Required]
    public string ConnectionString { get; set; } = "Data Source=phonebook;Mode=Memory;Cache=Shared";
}
