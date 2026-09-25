using PhoneBook.Application.Abstractions.Messaging;
using PhoneBook.Domain.Contacts;
using PhoneBook.Domain.Contacts.Services;
using PhoneBook.SharedKernel.Results;
using PhoneBook.SharedKernel.Time;

namespace PhoneBook.Application.Contacts.Create;

/// <summary>Creates a contact. Committing is done by <c>UnitOfWorkBehavior</c>, not here.</summary>
internal sealed class CreateContactCommandHandler(
    IContactRepository repository,
    ContactDuplicateChecker duplicateChecker,
    IDateTimeProvider clock)
    : ICommandHandler<CreateContactCommand, ContactResponse>
{
    public async Task<Result<ContactResponse>> Handle(CreateContactCommand command, CancellationToken cancellationToken)
    {
        var name = PersonName.Create(command.FirstName, command.LastName);
        var phone = PhoneNumber.Create(command.PhoneNumber);
        var tag = Tag.Create(command.Tag);

        var validation = Result.Combine(name, phone, tag);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        var duplicate = await duplicateChecker
            .EnsureNotDuplicateAsync(phone.Value, tag.Value, excluding: null, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate.IsFailure)
        {
            return duplicate.Error;
        }

        var contact = Contact.Create(name.Value, phone.Value, tag.Value, clock);
        if (contact.IsFailure)
        {
            return contact.Error;
        }

        repository.Add(contact.Value);
        return ContactResponse.FromDomain(contact.Value);
    }
}
