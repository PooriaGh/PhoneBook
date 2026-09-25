using PhoneBook.Application.Abstractions.Data.ReadModels;
using PhoneBook.Domain.Contacts;

namespace PhoneBook.Application.Contacts;

public sealed record ContactResponse(Guid Id, string FirstName, string LastName, string PhoneNumber, string Tag, int Version)
{
    public static ContactResponse FromDomain(Contact contact) => new(
        contact.Id.Value,
        contact.Name.FirstName,
        contact.Name.LastName,
        contact.Phone.Value,
        contact.Tag.Value,
        contact.Version);

    public static ContactResponse FromReadModel(ContactReadModel model) => new(
        model.Id, model.FirstName, model.LastName, model.PhoneNumber, model.Tag, model.Version);
}
