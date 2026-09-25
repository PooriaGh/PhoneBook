using System.Net;
using System.Net.Http.Json;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using static PhoneBook.Api.IntegrationTests.Infrastructure.HttpAssertions;

namespace PhoneBook.Api.IntegrationTests.RateLimiting;

/// <summary>
/// Feature 002, US2: the <c>api</c> policy. Each test gets a fresh host (research R-07) and its own callers.
/// </summary>
public sealed class ApiRateLimitTests : IAsyncLifetime
{
    private readonly RateLimitedApiFactory _factory = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Uri SearchUri => ContactsUri("?tag=work");

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Get_OverAllowance_Returns429ProblemWithRetryAfter()
    {
        using var client = NewCaller();
        await SendAllowedAsync(client);

        var response = await client.GetAsync(SearchUri, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "RateLimit.Exceeded");
        response.Headers.TryGetValues("Retry-After", out var values).ShouldBeTrue();
        int.Parse(values!.Single(), System.Globalization.CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(1);
        response.Headers.Any(h => h.Key.StartsWith("RateLimit", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
    }

    [Fact]
    public async Task Callers_AreLimitedIndependently()
    {
        using var noisy = NewCaller();
        using var polite = NewCaller();
        var noisyStatuses = new List<HttpStatusCode>();
        var politeStatuses = new List<HttpStatusCode>();

        // SC-003: the noisy caller sends 10 times its allowance while the polite one stays within its own.
        for (var i = 0; i < RateLimitedApiFactory.PermitLimit * 10; i++)
        {
            noisyStatuses.Add((await noisy.GetAsync(SearchUri, Ct)).StatusCode);
            if (i < RateLimitedApiFactory.PermitLimit)
            {
                politeStatuses.Add((await polite.GetAsync(SearchUri, Ct)).StatusCode);
            }
        }

        noisyStatuses.Skip(RateLimitedApiFactory.PermitLimit).ShouldAllBe(s => s == HttpStatusCode.TooManyRequests);
        politeStatuses.ShouldAllBe(s => s == HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_AfterRetryAfter_SucceedsAgain()
    {
        await using var shortWindow = new RateLimitedApiFactory(windowSeconds: 2);
        using var client = shortWindow.CreateClientWithScopes().WithSub(NewSub());
        await SendAllowedAsync(client);
        var limited = await client.GetAsync(SearchUri, Ct);
        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        var retryAfter = int.Parse(limited.Headers.GetValues("Retry-After").Single(), System.Globalization.CultureInfo.InvariantCulture);

        await Task.Delay(TimeSpan.FromSeconds(retryAfter) + TimeSpan.FromMilliseconds(200), Ct);

        (await client.GetAsync(SearchUri, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Anonymous_IsPartitionedByAddress_And429WinsOver401()
    {
        using var anonymous = _factory.CreateClientWithScopes(TestAuthHandler.NoScopes);

        for (var i = 0; i < RateLimitedApiFactory.PermitLimit; i++)
        {
            (await anonymous.GetAsync(SearchUri, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        (await anonymous.GetAsync(SearchUri, Ct)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Forbidden_OverAllowance_Returns429Not403()
    {
        using var readOnly = _factory.CreateClientWithScopes("phonebook.read").WithSub(NewSub());

        for (var i = 0; i < RateLimitedApiFactory.PermitLimit; i++)
        {
            (await readOnly.PostAsJsonAsync(ContactsUri(), ContactFaker.Request(), Ct)).StatusCode
                .ShouldBe(HttpStatusCode.Forbidden);
        }

        (await readOnly.PostAsJsonAsync(ContactsUri(), ContactFaker.Request(), Ct)).StatusCode
            .ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task WriteRoutes_CountAgainstTheSameAllowance()
    {
        using var client = NewCaller();
        for (var i = 0; i < RateLimitedApiFactory.PermitLimit - 1; i++)
        {
            (await client.GetAsync(SearchUri, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await client.PostAsJsonAsync(ContactsUri(), ContactFaker.Request(), Ct)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await client.PostAsJsonAsync(ContactsUri(), ContactFaker.Request(), Ct)).StatusCode
            .ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task HealthAndDocumentation_AreNeverLimited()
    {
        using var client = _factory.CreateClient();

        for (var i = 0; i < 50; i++)
        {
            (await client.GetAsync(new Uri("/health/live", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await client.GetAsync(new Uri("/health/ready", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await client.GetAsync(new Uri("/swagger/v1/swagger.json", UriKind.Relative), Ct)).StatusCode
                .ShouldNotBe(HttpStatusCode.TooManyRequests);
        }
    }

    private HttpClient NewCaller() => _factory.CreateClientWithScopes().WithSub(NewSub());

    private static string NewSub() => $"caller-{Guid.NewGuid():N}";

    private static async Task SendAllowedAsync(HttpClient client)
    {
        for (var i = 0; i < RateLimitedApiFactory.PermitLimit; i++)
        {
            (await client.GetAsync(SearchUri, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }
}
