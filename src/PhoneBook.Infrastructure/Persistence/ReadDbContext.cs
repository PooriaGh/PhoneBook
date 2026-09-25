using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhoneBook.Application.Abstractions.Data;
using PhoneBook.Application.Abstractions.Data.ReadModels;
using PhoneBook.Infrastructure.Persistence.Configurations;

namespace PhoneBook.Infrastructure.Persistence;

/// <summary>
/// Read side of CQRS: no tracking, read models only. Read-only by construction: saving does nothing
/// and logs a warning instead of throwing (research R-07).
/// </summary>
public sealed partial class ReadDbContext : DbContext, IReadDbContext
{
    private readonly ILogger<ReadDbContext> _logger;

    public ReadDbContext(DbContextOptions<ReadDbContext> options, ILogger<ReadDbContext> logger)
        : base(options)
    {
        _logger = logger;
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public IQueryable<ContactReadModel> Contacts => Set<ContactReadModel>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        LogSaveIgnored(_logger);
        return 0;
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        LogSaveIgnored(_logger);
        return Task.FromResult(0);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfiguration(new ContactReadModelConfiguration());

    [LoggerMessage(Level = LogLevel.Warning, Message = "SaveChanges was called on the read-only ReadDbContext and was ignored.")]
    private static partial void LogSaveIgnored(ILogger logger);
}
