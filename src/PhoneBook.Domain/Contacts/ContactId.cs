using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Domain.Contacts;

/// <summary>Strongly-typed identity of a <see cref="Contact"/> (time-ordered GUID v7).</summary>
public readonly record struct ContactId(Guid Value)
{
    public static ContactId New() => new(Guid.CreateVersion7());

    public static Result<ContactId> From(Guid value) =>
        value == Guid.Empty ? ContactErrors.IdEmpty : new ContactId(value);

    public override string ToString() => Value.ToString();
}
