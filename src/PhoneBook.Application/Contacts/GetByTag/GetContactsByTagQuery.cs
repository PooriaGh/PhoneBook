using PhoneBook.Application.Abstractions.Messaging;
using PhoneBook.Application.Abstractions.Paging;

namespace PhoneBook.Application.Contacts.GetByTag;

public sealed record GetContactsByTagQuery(
    string Tag,
    int Page = PagingDefaults.DefaultPage,
    int PageSize = PagingDefaults.DefaultPageSize) : IQuery<PagedResponse<ContactResponse>>;
