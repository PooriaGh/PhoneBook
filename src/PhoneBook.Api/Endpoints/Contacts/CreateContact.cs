using MediatR;
using PhoneBook.Api.Infrastructure;
using PhoneBook.Api.Infrastructure.Auth;
using PhoneBook.Application.Contacts;
using PhoneBook.Application.Contacts.Create;

namespace PhoneBook.Api.Endpoints.Contacts;

internal sealed class CreateContact : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(ContactRoutes.Collection, HandleAsync)
            .RequireAuthorization(Policies.ContactsWrite)
            .WithName("CreateContact")
            .WithTags(ContactRoutes.Tag)
            .WithSummary("Add a contact to the phone book")
            .Produces<ContactResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<IResult> HandleAsync(
        ContactRequest request, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var command = new CreateContactCommand(
            request.FirstName ?? string.Empty,
            request.LastName ?? string.Empty,
            request.PhoneNumber ?? string.Empty,
            request.Tag ?? string.Empty);

        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        httpContext.Response.SetETag(result.Value.Version);
        return TypedResults.CreatedAtRoute(result.Value, ContactRoutes.GetByIdName, new { id = result.Value.Id, version = "1" });
    }
}
