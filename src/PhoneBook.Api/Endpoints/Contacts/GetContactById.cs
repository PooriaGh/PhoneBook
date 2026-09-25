using MediatR;
using PhoneBook.Api.Infrastructure;
using PhoneBook.Api.Infrastructure.Auth;
using PhoneBook.Application.Contacts;
using PhoneBook.Application.Contacts.GetById;

namespace PhoneBook.Api.Endpoints.Contacts;

internal sealed class GetContactById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(ContactRoutes.Item, HandleAsync)
            .RequireAuthorization(Policies.ContactsRead)
            .WithName(ContactRoutes.GetByIdName)
            .WithTags(ContactRoutes.Tag)
            .WithSummary("Get one contact (served by ReadDbContext)")
            .Produces<ContactResponse>()
            .ProducesValidationProblem() // non-GUID ids are answered by the MalformedContactId fallback route
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<IResult> HandleAsync(
        Guid id, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetContactByIdQuery(id), cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        httpContext.Response.SetETag(result.Value.Version);
        return TypedResults.Ok(result.Value);
    }
}
