using System.Diagnostics.Metrics;

namespace PhoneBook.Application.Abstractions.Telemetry;

/// <summary>
/// The <c>PhoneBook</c> meter and its counters (feature 002, data-model §3). Created from the host's
/// <see cref="IMeterFactory"/>, so every host (and every test host) owns its own meter instance and its
/// measurements can be observed in isolation (research R-07).
/// </summary>
public sealed class PhoneBookMetrics
{
    public PhoneBookMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(PhoneBookTelemetry.MeterName);
        ContactsCreated = meter.CreateCounter<long>("phonebook.contacts.created", description: "Contacts created.");
        ContactsUpdated = meter.CreateCounter<long>("phonebook.contacts.updated", description: "Contacts updated.");
        ContactsDeleted = meter.CreateCounter<long>("phonebook.contacts.deleted", description: "Contacts deleted.");
        RateLimitRejections = meter.CreateCounter<long>(
            "phonebook.ratelimit.rejections", description: "Requests rejected by the rate limiter, tagged with the policy.");
    }

    public Counter<long> ContactsCreated { get; }

    public Counter<long> ContactsUpdated { get; }

    public Counter<long> ContactsDeleted { get; }

    public Counter<long> RateLimitRejections { get; }
}
