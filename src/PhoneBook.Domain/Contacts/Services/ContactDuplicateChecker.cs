using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Domain.Contacts.Services;

/// <summary>
/// Domain service for FR-018: the same phone number cannot be registered twice under the same tag.
/// A single aggregate cannot check this on its own, so it lives here.
/// </summary>
public sealed class ContactDuplicateChecker(IContactUniquenessReader reader)
{
    public async Task<Result> EnsureNotDuplicateAsync(
        PhoneNumber phone, Tag tag, ContactId? excluding, CancellationToken cancellationToken)
    {
        var exists = await reader.ExistsAsync(phone, tag, excluding, cancellationToken).ConfigureAwait(false);
        return exists ? Result.Failure(ContactErrors.Duplicate) : Result.Success();
    }
}
