using System.Diagnostics;
using Dapper;
using PhoneBook.Application.Abstractions.Data;
using PhoneBook.Application.Abstractions.Messaging;
using PhoneBook.Application.Abstractions.Paging;
using PhoneBook.Application.Abstractions.Telemetry;
using PhoneBook.Domain.Contacts;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Application.Contacts.GetByTag;

/// <summary>
/// Read side via Dapper (research R-07): one page of contacts carrying a tag (feature 002, research R-02).
/// </summary>
/// <remarks>
/// Both statements are parameterised and portable across SQLite and PostgreSQL (constitution Principle IV).
/// <c>ORDER BY last_name, first_name, id</c> gives byte order (Unicode code-point order) on both providers because
/// of their collation: SQLite's default <c>BINARY</c>, and PostgreSQL created with <c>LC_COLLATE=C</c> (a deployment
/// requirement). <c>id</c> is the final tie-break, so pages are deterministic.
/// </remarks>
internal sealed class GetContactsByTagQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetContactsByTagQuery, PagedResponse<ContactResponse>>
{
    private const string CountSql = "SELECT COUNT(*) FROM contacts WHERE normalized_tag = @NormalizedTag";

    private const string PageSql =
        "SELECT id AS Id, first_name AS FirstName, last_name AS LastName, phone_number AS PhoneNumber, " +
        "tag AS Tag, version AS Version FROM contacts WHERE normalized_tag = @NormalizedTag " +
        "ORDER BY last_name, first_name, id LIMIT @Take OFFSET @Skip";

    public async Task<Result<PagedResponse<ContactResponse>>> Handle(
        GetContactsByTagQuery query, CancellationToken cancellationToken)
    {
        var normalizedTag = Tag.Normalize(query.Tag);
        var skip = (long)(query.Page - 1) * query.PageSize;

        // Persistence span (feature 002, research R-04): also covers SQLite, which emits no database spans itself.
        // No parameter values are recorded (FR-016).
        using var activity = PhoneBookTelemetry.Persistence.StartActivity("query.contacts_by_tag", ActivityKind.Client);
        activity?.SetTag(PhoneBookTelemetry.DbSystemTag, connectionFactory.ProviderName);
        activity?.SetTag(PhoneBookTelemetry.DbOperationTag, "query.contacts_by_tag");

        var connection = connectionFactory.CreateConnection();
        await using (connection.ConfigureAwait(false))
        {
            var count = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                    CountSql, new { NormalizedTag = normalizedTag }, cancellationToken: cancellationToken))
                .ConfigureAwait(false);
            var totalCount = checked((int)count);

            IReadOnlyList<ContactResponse> items = [];
            if (totalCount > 0 && skip < totalCount)
            {
                var rows = await connection.QueryAsync<ContactRow>(new CommandDefinition(
                        PageSql,
                        new { NormalizedTag = normalizedTag, Take = query.PageSize, Skip = skip },
                        cancellationToken: cancellationToken))
                    .ConfigureAwait(false);
                items = rows
                    .Select(r => new ContactResponse(r.Id, r.FirstName, r.LastName, r.PhoneNumber, r.Tag, r.Version))
                    .ToList();
            }

            return Result<PagedResponse<ContactResponse>>.Success(
                new PagedResponse<ContactResponse>(items, query.Page, query.PageSize, totalCount));
        }
    }

    /// <summary>Dapper row with settable members, so provider type handlers (e.g. SQLite TEXT GUIDs) apply.</summary>
    private sealed class ContactRow
    {
        public Guid Id { get; init; }

        public string FirstName { get; init; } = string.Empty;

        public string LastName { get; init; } = string.Empty;

        public string PhoneNumber { get; init; } = string.Empty;

        public string Tag { get; init; } = string.Empty;

        public int Version { get; init; }
    }
}
