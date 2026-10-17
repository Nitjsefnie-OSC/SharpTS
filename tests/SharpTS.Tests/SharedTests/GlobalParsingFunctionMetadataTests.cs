using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class GlobalParsingFunctionMetadataTests
{
    public const string IssueSource = """
        console.log(parseInt.length, Number.parseInt.length, parseFloat.length, Number.parseFloat.length);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "2 2 1 1\n"];
        yield return ["canonical-aliases-descriptors-and-bound-arity", """
            const integer: any = parseInt, floating: any = parseFloat;
            const numberInteger: any = Number.parseInt, numberFloating: any = Number.parseFloat;
            console.log(integer === numberInteger, floating === numberFloating,
                integer.name, floating.name);
            for (const fn of [integer, numberInteger, floating, numberFloating]) {
                const descriptor = Object.getOwnPropertyDescriptor(fn, "length")!;
                console.log(fn.length, fn["length"], descriptor.value,
                    descriptor.writable, descriptor.enumerable, descriptor.configurable);
            }
            const boundInteger: any = integer.bind(null, "21"), boundFloating: any = floating.bind(null, "1.5");
            console.log(boundInteger.length, boundFloating.length, boundInteger(), boundFloating());
            """, "true true parseInt parseFloat\n2 2 2 false false true\n2 2 2 false false true\n"
            + "1 1 1 false false true\n1 1 1 false false true\n1 0 21 1.5\n"];
        yield return ["parsing-and-abrupt-arguments", """
            const integer: any = parseInt, floating: any = parseFloat;
            console.log(integer("12tail"), integer("ff", 16), integer("10", 2),
                floating("1.25tail"), Number.parseFloat("2.5tail"));
            const state = { calls: 0 }, marker = {};
            const input: any = { toString() { state.calls++; return "42tail"; } };
            console.log(integer(input, 10), state.calls);
            const bad: any = { toString() { throw marker; } };
            try { integer(bad, 10); } catch (error) { console.log(error === marker); }
            function fail(): any { throw marker; }
            for (const fn of [floating, Number.parseFloat]) {
                try { fn(fail()); } catch (error) { console.log(error === marker); }
            }
            console.log(Number.isNaN(integer("invalid")), Number.isNaN(floating("invalid")));
            """, "12 255 2 1.25 2.5\n42 1\ntrue\ntrue\ntrue\ntrue true\n"];
        yield return ["zero-argument-acceptance", """
            const integer: any = parseInt, floating: any = parseFloat;
            const numberInteger: any = Number.parseInt, numberFloating: any = Number.parseFloat;
            for (const fn of [integer, numberInteger, floating, numberFloating]) {
                console.log(Number.isNaN(fn()), Number.isNaN(fn(undefined)));
            }
            const boundInteger: any = integer.bind(null), boundFloating: any = floating.bind(null);
            console.log(Number.isNaN(boundInteger()), Number.isNaN(boundFloating()));
            """, "true true\ntrue true\ntrue true\ntrue true\ntrue true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void CanonicalParsingFunctionsExposeSpecArityWithoutRequiringArguments(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
