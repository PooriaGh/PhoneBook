using FluentValidation;
using PhoneBook.Application.Abstractions.Paging;
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

        // Feature 002 (FR-003, data-model §1): page ≥ 1; page size 1–200. Reported with the tag errors in one response.
        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(1)
            .WithName("page")
            .WithErrorCode(PagingDefaults.PageInvalid)
            .WithMessage("Page must be 1 or greater.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, PagingDefaults.MaxPageSize)
            .WithName("pageSize")
            .WithErrorCode(PagingDefaults.PageSizeInvalid)
            .WithMessage($"Page size must be between 1 and {PagingDefaults.MaxPageSize}.");
    }
}
