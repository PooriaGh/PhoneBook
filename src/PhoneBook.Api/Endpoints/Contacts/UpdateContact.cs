using MediatR;
using PhoneBook.Api.Infrastructure;
using PhoneBook.Api.Infrastructure.Auth;
using PhoneBook.Application.Contacts;
using PhoneBook.Application.Contacts.Update;

namespace PhoneBook.Api.Endpoints.Contacts;

internal sealed class UpdateContact : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut(ContactRoutes.Item, HandleAsync)
            .RequireAuthorization(Policies.ContactsWrite)
            .WithName("UpdateContact")
            .WithTags(ContactRoutes.Tag)
            .WithSummary("Replace a contact's editable fields (optional If-Match)")
            .Produces<ContactResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

    private static async Task<IResult> HandleAsync(
        Guid id, ContactRequest request, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var ifMatch = ETagExtensions.TryParseIfMatch(httpContext.Request);
        if (ifMatch.IsFailure)
        {
            return ifMatch.ToProblem();
        }

        var command = new UpdateContactCommand(
            id,
            request.FirstName ?? string.Empty,
            request.LastName ?? string.Empty,
            request.PhoneNumber ?? string.Empty,
            request.Tag ?? string.Empty,
            ifMatch.Value);

        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        httpContext.Response.SetETag(result.Value.Version);
        return TypedResults.Ok(result.Value);
    }
}
