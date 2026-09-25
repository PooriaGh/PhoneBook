using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;

namespace PhoneBook.Identity.IntegrationTests.Infrastructure;

/// <summary>
/// Identity host with extra settings (for example shortened lifetimes) and an optional environment, capturing logs.
/// Created per test, like the other non-shared hosts (feature 002, research R-07).
/// </summary>
public sealed class ConfiguredIdentityFactory(IReadOnlyDictionary<string, string> settings, string? environment = null)
    : IdentityFactory
{
    public LogCapture Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        if (environment is not null)
        {
            builder.UseEnvironment(environment);
        }

        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services => services.AddSingleton<ILogEventSink>(Logs));
    }
}
