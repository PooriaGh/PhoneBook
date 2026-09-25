using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PhoneBook.Application.Abstractions.Data;
using PhoneBook.Domain.Contacts;
using PhoneBook.SharedKernel.Time;

namespace PhoneBook.Api.IntegrationTests.Infrastructure;

/// <summary>Resets the database before each test and exposes ISender, an HTTP client and seeding helpers.</summary>
public abstract class BaseIntegrationTest(IPhoneBookApiFactory factory) : IAsyncLifetime
{
    private AsyncServiceScope _scope;

    protected IPhoneBookApiFactory Factory { get; } = factory;

    protected ISender Sender { get; private set; } = null!;

    protected HttpClient Client { get; private set; } = null!;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public virtual async ValueTask InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
        _scope = Factory.Services.CreateAsyncScope();
        Sender = _scope.ServiceProvider.GetRequiredService<ISender>();
        Client = Factory.CreateClientWithScopes();
    }

    public virtual async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _scope.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>Inserts a contact directly through the write side (no HTTP, no MediatR), so stories stay independent.</summary>
    protected async Task<Contact> SeedContactAsync(string firstName, string lastName, string phone, string tag)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contact = Contact.Create(
            PersonName.Create(firstName, lastName).Value,
            PhoneNumber.Create(phone).Value,
            Tag.Create(tag).Value,
            services.GetRequiredService<IDateTimeProvider>()).Value;

        services.GetRequiredService<IContactRepository>().Add(contact);
        var saved = await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        saved.IsSuccess.ShouldBeTrue(saved.Error.Code);
        return contact;
    }

    /// <summary>Runs a query against a fresh scope's read model (bypasses the API) for asserting persisted state.</summary>
    protected async Task<T> WithReadDbAsync<T>(Func<IReadDbContext, Task<T>> query)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<IReadDbContext>());
    }
}
