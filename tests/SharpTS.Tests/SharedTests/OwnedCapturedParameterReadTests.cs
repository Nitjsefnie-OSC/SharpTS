using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class OwnedCapturedParameterReadTests
{
    public const string Exact = """
        function middle(value: number) {
            function change() { value = value + 3; return value; }
            console.log(change(), value);
        }
        middle(10);
        """;

    public const string OwnerForms = """
        function outer() {
            function middle(value: number) {
                function change() { value = value + 3; return value; }
                console.log(change(), value);
            }
            middle(10);
        }
        const arrow = (value: number) => {
            const change = () => { value = value + 3; return value; };
            console.log(change(), value);
        };
        const expression = function(value: number) {
            function change() { value = value + 3; return value; }
            console.log(change(), value);
        };
        outer(); arrow(20); expression(30);
        """;

    public const string DefaultPrologue = """
        function declaration(value: number = 10, mirror: number = value + 1) {
            function change() { value = value + 3; return value; }
            console.log(change(), value, mirror);
        }
        const arrow = (value: number = 20, mirror: number = value + 1) => {
            const change = () => { value = value + 3; return value; };
            console.log(change(), value, mirror);
        };
        const expression = function(value: number = 30, mirror: number = value + 1) {
            const change = () => { value = value + 3; return value; };
            console.log(change(), value, mirror);
        };
        declaration(); arrow(); expression();
        """;

    public const string IdentityInvocations = """
        function declaration(value: any, replacement: any) {
            function change() { value = replacement; return value; }
            console.log(change() === replacement, value === replacement);
        }
        const arrow = (value: any, replacement: any) => {
            const change = () => { value = replacement; return value; };
            console.log(change() === replacement, value === replacement);
        };
        const first = { tag: 'first' }, second = { tag: 'second' }, third = { tag: 'third' };
        declaration(first, second); declaration(second, third);
        arrow(first, second); arrow(second, third);
        console.log(first.tag, second.tag, third.tag);
        """;

    public const string ControlAndShadow = """
        function parameter(value: number) {
            const read = () => value;
            value = value + 3;
            console.log(read(), value);
        }
        function local(initial: number) {
            let value = initial;
            function change() { value = value + 3; return value; }
            console.log(change(), value);
        }
        function shadow(value: number) {
            function change() { value = value + 3; return value; }
            const separate = (value: number) => { value = value + 1; return value; };
            console.log(separate(100), change(), value);
        }
        parameter(10); local(10); shadow(10);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["exact", Exact, "13 13\n"];
        yield return ["owner-forms", OwnerForms, "13 13\n23 23\n33 33\n"];
        yield return ["default-prologue", DefaultPrologue, "13 13 11\n23 23 21\n33 33 31\n"];
        yield return ["identity-invocations", IdentityInvocations, "true true\ntrue true\ntrue true\ntrue true\nfirst second third\n"];
        yield return ["control-and-shadow", ControlAndShadow, "13 13\n13 13\n101 13 13\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void OwnersReadTheCurrentCapturedParameterValue(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
