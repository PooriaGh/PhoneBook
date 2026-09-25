using Microsoft.AspNetCore.Http.HttpResults;

namespace PhoneBook.Api.Endpoints.Contacts;

/// <summary>
/// Spec edge case "malformed identifier": a non-GUID id is a bad request, not "not found". The route has no
/// constraint, so it ranks below the <c>{id:guid}</c> routes and only matches non-GUID ids. It still requires an
/// authenticated caller (constitution Principle VI, finding K3).
/// </summary>
internal sealed class MalformedContactId : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapMethods("contacts/{id}", [HttpMethods.Get, HttpMethods.Put, HttpMethods.Delete], Handle)
            .RequireAuthorization()
            .ExcludeFromDescription();

    private static ValidationProblem Handle(string id) =>
        TypedResults.ValidationProblem(
            new Dictionary<string, string[]>
            {
                ["id"] = [$"Contact.Id.Invalid: '{id}' is not a valid contact identifier (GUID expected)."],
            },
            title: "One or more validation errors occurred.",
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            extensions: new Dictionary<string, object?> { ["errorCode"] = "Contact.Id.Invalid" });
}
