using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using PhoneBook.Api.Infrastructure;
using PhoneBook.Api.Infrastructure.Auth;
using PhoneBook.Application.Abstractions.Paging;
using PhoneBook.Application.Contacts;
using PhoneBook.Application.Contacts.GetByTag;

namespace PhoneBook.Api.Endpoints.Contacts;

/// <summary>
/// Pages through the contacts carrying a tag. BREAKING (feature 002, FR-006): the response is a
/// <see cref="PagedResponse{T}"/> object instead of the former plain array.
/// </summary>
internal sealed class GetContactsByTag : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(ContactRoutes.Collection, HandleAsync)
            .RequireAuthorization(Policies.ContactsRead)
            .WithName("GetContactsByTag")
            .WithTags(ContactRoutes.Tag)
            .WithSummary("Page through the contacts carrying a tag (served by Dapper)")
            .Produces<PagedResponse<ContactResponse>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<IResult> HandleAsync(
        [FromQuery] string? tag,
        [FromQuery] string? page,
        [FromQuery] string? pageSize,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetContactsByTagQuery(
            tag ?? string.Empty,
            ParsePaging(page, PagingDefaults.DefaultPage),
            ParsePaging(pageSize, PagingDefaults.DefaultPageSize));

        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? result.ToProblem() : TypedResults.Ok(result.Value);
    }

    /// <summary>
    /// Absent or empty uses the default. Anything else that is not a plain non-negative integer (letters, a sign,
    /// overflow) becomes 0, so the validator reports the field's own error code instead of a framework binding error
    /// (data-model §1, "Parsing query values").
    /// </summary>
    private static int ParsePaging(string? value, int defaultValue) =>
        string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
