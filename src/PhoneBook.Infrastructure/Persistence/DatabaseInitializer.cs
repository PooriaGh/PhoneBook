using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PhoneBook.Infrastructure.Persistence;

/// <summary>
/// Creates the schema at startup. Migrations are unnecessary for a database that disappears on every restart
/// (research R-02). The indexes are plain SQL that runs unchanged on SQLite and PostgreSQL, because the
/// indexed columns belong to EF complex properties. Feature 002 replaces the tag-only index with
/// <c>ix_contacts_tag_order</c>, which also serves the paged <c>ORDER BY last_name, first_name, id</c>.
/// </summary>
internal sealed class DatabaseInitializer(IServiceProvider serviceProvider) : IHostedService
{
    private static readonly string[] IndexStatements =
    [
        "DROP INDEX IF EXISTS ix_contacts_normalized_tag",
        "CREATE INDEX IF NOT EXISTS ix_contacts_tag_order ON contacts (normalized_tag, last_name, first_name, id)",
        "CREATE UNIQUE INDEX IF NOT EXISTS ux_contacts_phone_tag ON contacts (phone_number, normalized_tag)",
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WriteDbContext>();

        // On SQLite, opening the keep-alive connection creates the in-memory database before the schema is created.
        if (dbContext.Database.IsSqlite())
        {
            _ = serviceProvider.GetRequiredService<InMemorySqliteKeepAlive>();
        }

        await dbContext.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        foreach (var statement in IndexStatements)
        {
#pragma warning disable EF1002 // Constant DDL, no user input.
            await dbContext.Database.ExecuteSqlRawAsync(statement, cancellationToken).ConfigureAwait(false);
#pragma warning restore EF1002
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
