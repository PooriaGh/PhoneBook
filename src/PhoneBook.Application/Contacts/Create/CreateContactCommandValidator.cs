using FluentValidation;

namespace PhoneBook.Application.Contacts.Create;

internal sealed class CreateContactCommandValidator : AbstractValidator<CreateContactCommand>
{
    public CreateContactCommandValidator() =>
        this.AddContactFieldRules(c => c.FirstName, c => c.LastName, c => c.PhoneNumber, c => c.Tag);
}
