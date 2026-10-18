using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class NestedExpressionGeneratorTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", """
            const outer = function () {
                function* values() { yield 1; }
                return values().next().value;
            };
            console.log(outer());
            """, "1\n"];
        yield return ["hoisting-and-completion", """
            const outer = function named() {
                const iterator = values();
                function* values() { yield 1; yield 2; return 3; }
                console.log(iterator.next().value, iterator.next().value);
                const final = iterator.next();
                console.log(final.value, final.done, iterator.next().done);
            };
            outer();
            """, "1 2\n3 true true\n"];
        yield return ["captured-parameter", """
            const outer = function (seed: number) {
                function* values() { yield seed; yield seed + 1; }
                return [...values()].join(",");
            };
            console.log(outer(4), outer(8));
            """, "4,5 8,9\n"];
        yield return ["arrow-and-recursion", """
            const arrow = () => { function* values() { yield 6; } return values().next().value; };
            const ordinary = function () {
                function* values(n: number) { if (n > 0) yield* values(n - 1); yield n; }
                return [...values(2)].join(",");
            };
            console.log(arrow(), ordinary());
            """, "6 0,1,2\n"];
        yield return ["async-generator", """
            const outer = function () {
                async function* values() { yield 1; yield 2; }
                return values();
            };
            const iterator = outer();
            iterator.next().then((result: any) => console.log(result.value, result.done));
            iterator.next().then((result: any) => console.log(result.value, result.done));
            """, "1 false\n2 false\n"];
        yield return ["other-enclosures", """
            function declaration() { function* values() { yield 3; } return values().next().value; }
            function* generator() { function* values() { yield 4; } yield* values(); }
            function capture() {
                { const value = 5; const values = function* () { yield value; }; return values().next().value; }
            }
            console.log(declaration(), generator().next().value, capture());
            """, "3 4 5\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void GeneratorDeclarationsInsideFunctionExpressionsUseGeneratorLowering(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
