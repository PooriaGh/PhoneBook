using FluentValidation;
using PhoneBook.Domain.Contacts;

namespace PhoneBook.Application.Contacts.GetByTag;

internal sealed class GetContactsByTagQueryValidator : AbstractValidator<GetContactsByTagQuery>
{
    public GetContactsByTagQueryValidator()
    {
        RuleFor(q => q.Tag)
            .Must(tag => !string.IsNullOrWhiteSpace(tag))
            .WithName("tag")
            .WithErrorCode("Tag.Required")
            .WithMessage("Tag is required.")
            .Must(tag => tag is null || tag.Trim().Length <= Tag.MaxLength)
            .WithName("tag")
            .WithErrorCode("Tag.TooLong")
            .WithMessage($"Tag must be at most {Tag.MaxLength} characters.");
    }
}
