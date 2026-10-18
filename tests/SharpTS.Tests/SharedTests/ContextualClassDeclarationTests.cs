using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ContextualClassDeclarationTests
{
    [Theory, ModeData]
    public void TypeNamedClassSupportsStaticAndInstanceMembers(ExecutionMode mode)
    {
        const string source = """
            class type {
                static create(): type { return new type(); }
                instanceMethod() { return 42; }
            }
            const instance: type = type.create();
            console.log(instance.instanceMethod(), instance instanceof type);
            """;
        Assert.Equal("42 true\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ContextualNamesRemainUsableAsClassValues(ExecutionMode mode)
    {
        const string source = """
            class namespace { static value() { return 1; } }
            class readonly { static value() { return 2; } }
            class override { static value() { return 3; } }
            class as { static value() { return 4; } instanceMethod() { return 5; } }
            class using { static value() { return 6; } instanceMethod() { return 7; } }
            console.log(namespace.value(), readonly.value(), override.value(), as.value(),
                new as().instanceMethod(), using.value(), new using().instanceMethod());
            const value = new as() as as;
            console.log(value instanceof as, new using() instanceof using);
            """;
        Assert.Equal("1 2 3 4 5 6 7\ntrue true\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void UsingDeclarationsRemainDistinctFromClassReferences(ExecutionMode mode)
    {
        const string source = """
            class using { static value() { return 8; } }
            const events: string[] = [];
            {
                using resource = { [Symbol.dispose]() { events.push("disposed"); } };
                { using as: any = null; using using: any = null; }
                events.push("body");
                console.log(using.value());
                using as typeof using;
            }
            console.log(events.join(","));
            """;
        Assert.Equal("8\nbody,disposed\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void UsingFollowedByANewlineRemainsAClassReference(ExecutionMode mode)
    {
        const string source = """
            class using {}
            let resource: any = null;
            const events: string[] = [];
            {
                using
                resource = { [Symbol.dispose]() { events.push("disposed"); } };
                events.push("body");
            }
            console.log(events.join(","), resource !== null);
            """;
        Assert.Equal("body true\n", TestHarness.Run(source, mode));
    }
}
