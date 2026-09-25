namespace PhoneBook.SharedKernel.Time;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
