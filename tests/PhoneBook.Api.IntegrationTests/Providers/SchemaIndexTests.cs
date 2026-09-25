using Dapper;
using Microsoft.Extensions.DependencyInjection;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using PhoneBook.Application.Abstractions.Data;

namespace PhoneBook.Api.IntegrationTests.Providers;

/// <summary>Feature 002, research R-02: the paging index replaces the old tag-only index on both providers.</summary>
public abstract class SchemaIndexTestsBase(IPhoneBookApiFactory factory, string indexQuery)
{
    [Fact]
    public async Task Schema_HasPagingIndexAndNoTagOnlyIndex()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await using var connection = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().CreateConnection();

        var indexes = (await connection.QueryAsync<string>(indexQuery)).ToList();

        indexes.ShouldContain("ix_contacts_tag_order");
        indexes.ShouldNotContain("ix_contacts_normalized_tag");
        indexes.ShouldContain("ux_contacts_phone_tag");
    }
}

[Collection(IntegrationTestCollection.Name)]
public sealed class PostgresSchemaIndexTests(PhoneBookApiFactory factory)
    : SchemaIndexTestsBase(factory, "SELECT indexname FROM pg_indexes WHERE tablename = 'contacts'");

[Collection(SqliteIntegrationTestCollection.Name)]
public sealed class SqliteSchemaIndexTests(SqliteApiFactory factory)
    : SchemaIndexTestsBase(factory, "SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = 'contacts'");
