using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PhoneBook.Application.Abstractions.Data;
using PhoneBook.Domain.Contacts;
using PhoneBook.Domain.Contacts.Services;
using PhoneBook.Infrastructure.Persistence;
using PhoneBook.Infrastructure.Persistence.Dapper;
using PhoneBook.Infrastructure.Persistence.Repositories;
using PhoneBook.Infrastructure.Time;
using PhoneBook.SharedKernel.Time;

namespace PhoneBook.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // The provider is resolved from options at runtime (not while registering services), so hosts and test
        // factories can override Database:* configuration. SQLite-only services are simply unused on PostgreSQL.
        services.AddSingleton<InMemorySqliteKeepAlive>();
        services.AddSingleton<SqliteWriteGate>();

        services.AddDbContext<WriteDbContext>((sp, options) => Configure(sp, options));
        services.AddDbContext<ReadDbContext>((sp, options) => Configure(sp, options));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<WriteDbContext>());
        services.AddScoped<IReadDbContext>(sp => sp.GetRequiredService<ReadDbContext>());
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        SqlMapper.AddTypeHandler(new GuidTypeHandler());
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IContactUniquenessReader, ContactUniquenessReader>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.AddHostedService<DatabaseInitializer>();
        services.AddHealthChecks().AddDbContextCheck<WriteDbContext>("database", tags: ["ready"]);

        return services;
    }

    private static void Configure(IServiceProvider serviceProvider, DbContextOptionsBuilder options)
    {
        var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        if (databaseOptions.Provider == DatabaseProvider.Postgres)
        {
            options.UseNpgsql(databaseOptions.ConnectionString);
        }
        else
        {
            options.UseSqlite(databaseOptions.ConnectionString);
        }

        options.UseSnakeCaseNamingConvention();
    }
}
