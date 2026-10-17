using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class OrdinaryConstructorStructuralTests
{
    public const string IssueSource = """
        const First = class Same {
            static #m = 42;
            static take(value: typeof Same) { return value; }
        };
        const Other = class Same {
            static #m = 43;
            static take(value: typeof Same) { return value; }
        };
        console.log(First.take(Other) === Other);
        """;

    public const string SelectedConstructorSource = """
        class Target {
            static tag: string = "target";
            value: number;
            constructor(value: number) { this.value = value; }
            read(): number { return this.value; }
        }
        class Source {
            static tag: string = "source";
            value: number;
            constructor(value: number) { this.value = value + 1; }
            read(): number { return this.value; }
        }
        function choose(value: typeof Target): typeof Target { return value; }
        let Alias: typeof Target = Target;
        Alias = choose(Source);
        const selected = new Alias(7);
        console.log(Alias === Source, Alias !== Target, Alias.tag, selected.read(),
            selected instanceof Source, selected instanceof Target);
        """;

    public const string SelectedIdentitySource = """
        class Target { static tag: string = "target"; }
        class Source { static tag: string = "source"; }
        function choose(value: typeof Target): typeof Target { return value; }
        let Alias: typeof Target = Target;
        Alias = choose(Source);
        console.log(Alias === Source, Alias !== Target, Alias.tag);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "true\n"];
        yield return ["selected-identity", SelectedIdentitySource, "true true source\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void StructurallyAcceptedConstructorValuesRetainTheirOwnIdentityAndConstruction(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Fact]
    public void SelectedConstructorAliasConstructsTheSelectedClass()
    {
        // Compiled typed-alias construction selects the annotation's class (#2339).
        Assert.Equal("true true source 8 true false\n",
            TestHarness.Run(SelectedConstructorSource, ExecutionMode.Interpreted));
    }
}
