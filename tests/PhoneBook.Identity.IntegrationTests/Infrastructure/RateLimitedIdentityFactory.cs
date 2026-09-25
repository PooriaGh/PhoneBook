namespace PhoneBook.Identity.IntegrationTests.Infrastructure;

/// <summary>
/// Identity host whose <c>token</c> policy allows only 3 requests per window. Not a fixture: every in-process request
/// shares the partition <c>ip:unknown</c>, so each test creates its own instance (feature 002, research R-07).
/// </summary>
public sealed class RateLimitedIdentityFactory : IdentityFactory
{
    public const int Limit = 3;

    protected override int TokenPermitLimit => Limit;
}
