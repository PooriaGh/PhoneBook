namespace PhoneBook.Application.Abstractions.Paging;

/// <summary>Paging defaults, limits and error codes (feature 002, FR-002, FR-003, data-model §1).</summary>
public static class PagingDefaults
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public const string PageInvalid = "Paging.Page.Invalid";
    public const string PageSizeInvalid = "Paging.PageSize.Invalid";
}
