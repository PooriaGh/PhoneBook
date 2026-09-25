using PhoneBook.Application.Abstractions.Messaging;
using PhoneBook.Domain.Contacts;
using PhoneBook.Domain.Contacts.Services;
using PhoneBook.SharedKernel.Results;
using PhoneBook.SharedKernel.Time;

namespace PhoneBook.Application.Contacts.Update;

internal sealed class UpdateContactCommandHandler(
    IContactRepository repository,
    ContactDuplicateChecker duplicateChecker,
    IDateTimeProvider clock)
    : ICommandHandler<UpdateContactCommand, ContactResponse>
{
    public async Task<Result<ContactResponse>> Handle(UpdateContactCommand command, CancellationToken cancellationToken)
    {
        var name = PersonName.Create(command.FirstName, command.LastName);
        var phone = PhoneNumber.Create(command.PhoneNumber);
        var tag = Tag.Create(command.Tag);

        var validation = Result.Combine(name, phone, tag);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        var id = new ContactId(command.Id);
        var contact = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (contact is null)
        {
            return ContactErrors.NotFound(id);
        }

        if (command.ExpectedVersion is { } expected && expected != contact.Version)
        {
            return ContactErrors.VersionMismatch;
        }

        var duplicate = await duplicateChecker
            .EnsureNotDuplicateAsync(phone.Value, tag.Value, excluding: contact.Id, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate.IsFailure)
        {
            return duplicate.Error;
        }

        var updated = contact.Update(name.Value, phone.Value, tag.Value, clock);
        return updated.IsFailure ? updated.Error : ContactResponse.FromDomain(contact);
    }
}
