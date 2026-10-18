using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ConstructorParameterArrowMutationTests
{
    public const string ExactConstructorArrowMutation = """
        class C {
            constructor(value: number) {
                const change = () => { value += 3; };
                change();
                console.log(value);
            }
        }
        new C(44);
        """;

    public const string DerivedBeforeSuperArrowMutation = """
        class Base {}
        class Derived extends Base {
            constructor(public value: number) {
                const change = () => { value += 3; };
                change();
                console.log(value);
                super();
            }
        }
        console.log(new Derived(44).value);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["exact-constructor-arrow-mutation", ExactConstructorArrowMutation, "47\n"];
        yield return ["derived-before-super-arrow-mutation", DerivedBeforeSuperArrowMutation, "47\n47\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void CalledArrowMutationIsVisibleToConstructorAndParameterProperty(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
