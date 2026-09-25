using MediatR;
using Microsoft.AspNetCore.Mvc;
using PhoneBook.Api.Infrastructure;
using PhoneBook.Api.Infrastructure.Auth;
using PhoneBook.Application.Contacts;
using PhoneBook.Application.Contacts.GetByTag;

namespace PhoneBook.Api.Endpoints.Contacts;

internal sealed class GetContactsByTag : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(ContactRoutes.Collection, HandleAsync)
            .RequireAuthorization(Policies.ContactsRead)
            .WithName("GetContactsByTag")
            .WithTags(ContactRoutes.Tag)
            .WithSummary("Get all contacts carrying a tag (served by Dapper)")
            .Produces<IReadOnlyList<ContactResponse>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

    private static async Task<IResult> HandleAsync(
        [FromQuery] string? tag, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetContactsByTagQuery(tag ?? string.Empty), cancellationToken)
            .ConfigureAwait(false);
        return result.IsFailure ? result.ToProblem() : TypedResults.Ok(result.Value);
    }
}
