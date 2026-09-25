using System.Reflection;
using PhoneBook.Api;
using PhoneBook.Domain.Contacts;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.ArchitectureTests;

internal static class Layers
{
    public static readonly Assembly SharedKernelAssembly = typeof(Result).Assembly;
    public static readonly Assembly DomainAssembly = typeof(Contact).Assembly;
    public static readonly Assembly ApplicationAssembly = typeof(PhoneBook.Application.DependencyInjection).Assembly;
    public static readonly Assembly InfrastructureAssembly = typeof(PhoneBook.Infrastructure.DependencyInjection).Assembly;
    public static readonly Assembly ApiAssembly = typeof(ApiAssemblyMarker).Assembly;

    public const string SharedKernelNamespace = "PhoneBook.SharedKernel";
    public const string DomainNamespace = "PhoneBook.Domain";
    public const string ApplicationNamespace = "PhoneBook.Application";
    public const string InfrastructureNamespace = "PhoneBook.Infrastructure";
    public const string ApiNamespace = "PhoneBook.Api";
}
