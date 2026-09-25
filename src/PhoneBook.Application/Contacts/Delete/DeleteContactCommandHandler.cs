using PhoneBook.Application.Abstractions.Messaging;
using PhoneBook.Domain.Contacts;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Application.Contacts.Delete;

internal sealed class DeleteContactCommandHandler(IContactRepository repository)
    : ICommandHandler<DeleteContactCommand>
{
    public async Task<Result> Handle(DeleteContactCommand command, CancellationToken cancellationToken)
    {
        var id = new ContactId(command.Id);
        var contact = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (contact is null)
        {
            return Result.Failure(ContactErrors.NotFound(id));
        }

        if (command.ExpectedVersion is { } expected && expected != contact.Version)
        {
            return Result.Failure(ContactErrors.VersionMismatch);
        }

        var deleted = contact.MarkAsDeleted();
        if (deleted.IsFailure)
        {
            return deleted;
        }

        // Events raised above are still collected: WriteDbContext reads them from Deleted entries too.
        repository.Remove(contact);
        return Result.Success();
    }
}
