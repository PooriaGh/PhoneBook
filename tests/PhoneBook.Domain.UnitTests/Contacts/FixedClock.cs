using PhoneBook.SharedKernel.Time;

namespace PhoneBook.Domain.UnitTests.Contacts;

internal sealed class FixedClock(DateTime utcNow) : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = utcNow;
}
