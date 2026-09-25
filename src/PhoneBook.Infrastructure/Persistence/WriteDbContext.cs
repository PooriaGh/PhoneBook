using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PhoneBook.Application.Abstractions.Data;
using PhoneBook.Application.Abstractions.Events;
using PhoneBook.Application.Abstractions.Telemetry;
using PhoneBook.Domain.Contacts;
using PhoneBook.Infrastructure.Persistence.Configurations;
using PhoneBook.SharedKernel.Domain;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Infrastructure.Persistence;

/// <summary>
/// Write side of CQRS and the Unit of Work. Persistence conflicts are translated to <see cref="Result"/>
/// failures here (constitution Principle II), and domain events are published only after commit.
/// </summary>
public sealed class WriteDbContext(
    DbContextOptions<WriteDbContext> options,
    IDomainEventDispatcher domainEventDispatcher,
    SqliteWriteGate sqliteWriteGate)
    : DbContext(options), IUnitOfWork
{
    private const int SqliteConstraintUnique = 2067;

    public DbSet<Contact> Contacts => Set<Contact>();

    async Task<Result> IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        var aggregates = ChangeTracker.Entries<IHasDomainEvents>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();
        var domainEvents = aggregates.SelectMany(a => a.DomainEvents).ToList();
        aggregates.ForEach(a => a.ClearDomainEvents());

        // Persistence span (feature 002, research R-04); the failure path is the existing Result translation.
        using (var activity = PhoneBookTelemetry.Persistence.StartActivity("save", ActivityKind.Client))
        {
            activity?.SetTag(PhoneBookTelemetry.DbSystemTag, Database.IsNpgsql() ? "postgresql" : "sqlite");
            activity?.SetTag(PhoneBookTelemetry.DbOperationTag, "save");

            var saveResult = await SaveWithGateAsync(cancellationToken).ConfigureAwait(false);
            if (saveResult.IsFailure)
            {
                activity?.SetStatus(ActivityStatusCode.Error, saveResult.Error.Code);
                return saveResult;
            }
        }

        await domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfiguration(new ContactConfiguration());

    private async Task<Result> SaveWithGateAsync(CancellationToken cancellationToken)
    {
        // Writes are serialized only on SQLite (finding P2); PostgreSQL handles concurrency itself.
        using var gate = Database.IsSqlite()
            ? await sqliteWriteGate.AcquireAsync(cancellationToken).ConfigureAwait(false)
            : null;

        // Persistence-to-Result translation: one of the catch sites constitution Principle II allows.
        try
        {
            await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(ContactErrors.ConcurrencyConflict);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Result.Failure(ContactErrors.Duplicate);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) => exception.InnerException switch
    {
        SqliteException sqlite => sqlite.SqliteExtendedErrorCode == SqliteConstraintUnique,
        PostgresException postgres => postgres.SqlState == PostgresErrorCodes.UniqueViolation,
        _ => false,
    };
}
