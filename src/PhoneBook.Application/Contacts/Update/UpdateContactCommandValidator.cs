using FluentValidation;

namespace PhoneBook.Application.Contacts.Update;

internal sealed class UpdateContactCommandValidator : AbstractValidator<UpdateContactCommand>
{
    public UpdateContactCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty().WithErrorCode("Contact.Id.Empty").WithMessage("Contact id must not be empty.");
        RuleFor(c => c.ExpectedVersion)
            .GreaterThan(0)
            .When(c => c.ExpectedVersion.HasValue)
            .WithErrorCode("Request.IfMatch.Invalid")
            .WithMessage("If-Match must be a positive version.");

        this.AddContactFieldRules(c => c.FirstName, c => c.LastName, c => c.PhoneNumber, c => c.Tag);
    }
}
