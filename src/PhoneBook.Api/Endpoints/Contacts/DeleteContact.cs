using MediatR;
using PhoneBook.Api.Infrastructure;
using PhoneBook.Api.Infrastructure.Auth;
using PhoneBook.Application.Contacts.Delete;

namespace PhoneBook.Api.Endpoints.Contacts;

internal sealed class DeleteContact : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete(ContactRoutes.Item, HandleAsync)
            .RequireAuthorization(Policies.ContactsWrite)
            .WithName("DeleteContact")
            .WithTags(ContactRoutes.Tag)
            .WithSummary("Delete a contact (optional If-Match)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

    private static async Task<IResult> HandleAsync(
        Guid id, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var ifMatch = ETagExtensions.TryParseIfMatch(httpContext.Request);
        if (ifMatch.IsFailure)
        {
            return ifMatch.ToProblem();
        }

        var result = await sender.Send(new DeleteContactCommand(id, ifMatch.Value), cancellationToken)
            .ConfigureAwait(false);
        return result.IsFailure ? result.ToProblem() : TypedResults.NoContent();
    }
}
