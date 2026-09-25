namespace PhoneBook.Application.Abstractions.Messaging;

/// <summary>Marker for write requests; the Unit of Work behaviour commits only these.</summary>
#pragma warning disable CA1040 // Marker interface is intentional.
public interface IBaseCommand;
#pragma warning restore CA1040
