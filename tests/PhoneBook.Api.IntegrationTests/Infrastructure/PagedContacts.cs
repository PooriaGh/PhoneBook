using PhoneBook.Application.Contacts;

namespace PhoneBook.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The paged tag-search body. Feature 002 replaced the plain array in place (FR-006, a deliberate breaking change);
/// the expected contacts and their order are unchanged (SC-008).
/// </summary>
internal sealed record PagedContacts(List<ContactResponse> Items, int Page, int PageSize, int TotalCount, bool HasNext);
