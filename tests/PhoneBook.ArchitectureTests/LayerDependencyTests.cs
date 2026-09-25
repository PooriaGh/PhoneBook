using NetArchTest.Rules;
using static PhoneBook.ArchitectureTests.Layers;

namespace PhoneBook.ArchitectureTests;

/// <summary>Enforces the Clean Architecture dependency rule and constitution Principles I and IV.</summary>
public sealed class LayerDependencyTests
{
    [Fact]
    public void SharedKernel_DependsOnNoOtherProjectOrFramework() =>
        AssertNoDependency(Types.InAssembly(SharedKernelAssembly),
            DomainNamespace, ApplicationNamespace, InfrastructureNamespace, ApiNamespace,
            "MediatR", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore");

    [Fact]
    public void Domain_DependsOnlyOnSharedKernel() =>
        AssertNoDependency(Types.InAssembly(DomainAssembly),
            ApplicationNamespace, InfrastructureNamespace, ApiNamespace,
            "MediatR", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "FluentValidation", "Dapper");

    [Fact]
    public void Application_DoesNotDependOnInfrastructureApiOrProviderDrivers() =>
        // Dapper and EF Core query operators are allowed on the read side; provider drivers are not.
        AssertNoDependency(Types.InAssembly(ApplicationAssembly),
            InfrastructureNamespace, ApiNamespace,
            "Microsoft.AspNetCore", "Npgsql", "Microsoft.Data.Sqlite", "Microsoft.EntityFrameworkCore.Sqlite");

    [Fact]
    public void Infrastructure_DoesNotDependOnApi() =>
        AssertNoDependency(Types.InAssembly(InfrastructureAssembly), ApiNamespace);

    [Fact]
    public void QueryHandlers_DoNotUseWriteSide()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().HaveNameEndingWith("QueryHandler")
            .ShouldNot().HaveDependencyOnAny(
                "PhoneBook.Domain.Contacts.IContactRepository",
                "PhoneBook.Application.Abstractions.Data.IUnitOfWork",
                "PhoneBook.Infrastructure.Persistence.WriteDbContext")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    private static void AssertNoDependency(Types types, params string[] forbidden)
    {
        var result = types.ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        "Violating types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
