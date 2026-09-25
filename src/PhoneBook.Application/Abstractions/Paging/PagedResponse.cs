namespace PhoneBook.Application.Abstractions.Paging;

/// <summary>One page of results plus paging information (feature 002, FR-001, data-model §1).</summary>
/// <remarks>
/// A page beyond the last one is empty but still reports <see cref="TotalCount"/>. <see cref="HasNext"/> uses
/// 64-bit arithmetic so a huge page number cannot overflow.
/// </remarks>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public bool HasNext => (long)Page * PageSize < TotalCount;
}
