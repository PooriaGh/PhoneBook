using Dapper;
using PhoneBook.Application.Abstractions.Data;
using PhoneBook.Application.Abstractions.Messaging;
using PhoneBook.Domain.Contacts;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Application.Contacts.GetByTag;

/// <summary>
/// Read side via Dapper (research R-07). The SQL is parameterised and portable across SQLite and PostgreSQL, and
/// deliberately has no ORDER BY: the providers collate text differently, so results are sorted in memory with
/// ordinal comparison to get the same order everywhere (FR-008, finding P1).
/// </summary>
internal sealed class GetContactsByTagQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetContactsByTagQuery, IReadOnlyList<ContactResponse>>
{
    private const string Sql =
        "SELECT id AS Id, first_name AS FirstName, last_name AS LastName, phone_number AS PhoneNumber, " +
        "tag AS Tag, version AS Version FROM contacts WHERE normalized_tag = @NormalizedTag";

    public async Task<Result<IReadOnlyList<ContactResponse>>> Handle(
        GetContactsByTagQuery query, CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();
        await using (connection.ConfigureAwait(false))
        {
            var rows = await connection.QueryAsync<ContactRow>(new CommandDefinition(
                    Sql, new { NormalizedTag = Tag.Normalize(query.Tag) }, cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            IReadOnlyList<ContactResponse> contacts = rows
                .OrderBy(r => r.LastName, StringComparer.Ordinal)
                .ThenBy(r => r.FirstName, StringComparer.Ordinal)
                .Select(r => new ContactResponse(r.Id, r.FirstName, r.LastName, r.PhoneNumber, r.Tag, r.Version))
                .ToList();

            return Result<IReadOnlyList<ContactResponse>>.Success(contacts);
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
