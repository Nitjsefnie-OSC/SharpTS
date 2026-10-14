using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class AsyncStableIteratorResultTests
{
    private const string Iterator = """
        let current: number = 0;
        const iterable = {
            [Symbol.iterator]() { return this; },
            next() {
                if (current < 2) {
                    const value: number = current + 4;
                    current = current + 1;
                    return { value, done: false };
                }
                return { value: 0, done: true };
            }
        };
        let total = 0, count = 0;
        """;

    private const string Loop = """
        for (const value of iterable) {
            total += value;
            if (++count === 5) break;
        }
        console.log(total, count);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["function", $$"""
            async function run() { {{Iterator}} {{Loop}} }
            run();
            """, "9 2\n"];
        yield return ["arrow", $$"""
            const run = async () => { {{Iterator}} {{Loop}} };
            run();
            """, "9 2\n"];
        yield return ["labels", $$"""
            async function run() {
                {{Iterator}}
                outer: inner: for (const value of iterable) {
                    total += value;
                    if (++count === 5) break inner;
                    if (value === 4) continue outer;
                }
                console.log(total, count);
            }
            run();
            """, "9 2\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void GenericAsyncConsumersReadStableResultValuesAndExhaustNaturally(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled) Assert.Empty(TestHarness.CompileAndVerifyOnly(source));
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
