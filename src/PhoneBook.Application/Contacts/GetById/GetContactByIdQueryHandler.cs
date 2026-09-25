using Microsoft.EntityFrameworkCore;
using PhoneBook.Application.Abstractions.Data;
using PhoneBook.Application.Abstractions.Messaging;
using PhoneBook.Domain.Contacts;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Application.Contacts.GetById;

/// <summary>Read side via <see cref="IReadDbContext"/>: a no-tracking LINQ projection of the read model.</summary>
internal sealed class GetContactByIdQueryHandler(IReadDbContext readDb)
    : IQueryHandler<GetContactByIdQuery, ContactResponse>
{
    public async Task<Result<ContactResponse>> Handle(GetContactByIdQuery query, CancellationToken cancellationToken)
    {
        var contact = await readDb.Contacts
            .Where(c => c.Id == query.Id)
            .Select(c => new ContactResponse(c.Id, c.FirstName, c.LastName, c.PhoneNumber, c.Tag, c.Version))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return contact is null ? ContactErrors.NotFound(new ContactId(query.Id)) : contact;
    }
}
