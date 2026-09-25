using FluentValidation;
using NetArchTest.Rules;
using PhoneBook.SharedKernel.Domain;
using static PhoneBook.ArchitectureTests.Layers;

namespace PhoneBook.ArchitectureTests;

public sealed class NamingConventionTests
{
    [Fact]
    public void CommandHandlers_EndWithCommandHandler()
    {
        var handlers = ApplicationAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition().Name.StartsWith("ICommandHandler", StringComparison.Ordinal)))
            .ToList();

        handlers.ShouldNotBeEmpty();
        handlers.ShouldAllBe(t => t.Name.EndsWith("CommandHandler", StringComparison.Ordinal));
    }

    [Fact]
    public void Validators_EndWithValidator()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().Inherit(typeof(AbstractValidator<>))
            .Should().HaveNameEndingWith("Validator")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void DomainEvents_AreSealedAndEndWithDomainEvent()
    {
        var events = DomainAssembly.GetTypes().Where(t => t.IsSubclassOf(typeof(DomainEvent))).ToList();

        events.ShouldNotBeEmpty();
        events.ShouldAllBe(t => t.IsSealed && t.Name.EndsWith("DomainEvent", StringComparison.Ordinal));
    }
}
