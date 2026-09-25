namespace PhoneBook.Api.Endpoints;

/// <summary>One HTTP operation. Implementations are discovered by assembly scanning.</summary>
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
