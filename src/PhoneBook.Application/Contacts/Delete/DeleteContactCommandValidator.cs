using FluentValidation;

namespace PhoneBook.Application.Contacts.Delete;

internal sealed class DeleteContactCommandValidator : AbstractValidator<DeleteContactCommand>
{
    public DeleteContactCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty().WithErrorCode("Contact.Id.Empty").WithMessage("Contact id must not be empty.");
        RuleFor(c => c.ExpectedVersion)
            .GreaterThan(0)
            .When(c => c.ExpectedVersion.HasValue)
            .WithErrorCode("Request.IfMatch.Invalid")
            .WithMessage("If-Match must be a positive version.");
    }
}
