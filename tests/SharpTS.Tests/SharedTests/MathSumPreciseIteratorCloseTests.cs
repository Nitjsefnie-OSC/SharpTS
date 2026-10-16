using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class MathSumPreciseIteratorCloseTests
{
    public const string NativeGeneratorSource = """
        const math: any = Math;
        let expectedMessage = "";
        try { math.sumPrecise(["invalid"]); }
        catch (error: any) { expectedMessage = error.message; }
        function check(failingCleanup: boolean) {
            const state = { events: "" };
            function* values() {
                try {
                    state.events += "first;"; yield 1;
                    state.events += "invalid;"; yield "invalid";
                    state.events += "resumed;"; yield 2;
                } finally {
                    state.events += "close;";
                    if (failingCleanup) throw "cleanup";
                }
            }
            try { math.sumPrecise(values()); console.log("returned"); }
            catch (error: any) {
                console.log(failingCleanup, error instanceof TypeError,
                    error.message === expectedMessage, state.events);
            }
        }
        check(false);
        check(true);
        """;

    public const string NativeGeneratorExpected = "false true true first;invalid;close;\n"
        + "true true true first;invalid;close;\n";

    public const string CustomIteratorSource = """
        const math: any = Math;
        let expectedMessage = "";
        try { math.sumPrecise(["invalid"]); }
        catch (error: any) { expectedMessage = error.message; }
        function check(kind: number) {
            const state = { next: 0, returnGets: 0, returnCalls: 0, sourceReturns: 0,
                terminalValues: 0, receivers: true };
            const iterator: any = {
                next() {
                    state.receivers = state.receivers && this === iterator;
                    state.next++;
                    if (state.next === 1) return { done: false, value: 1 };
                    if (state.next === 2) return { done: false, value: kind === 5 ? 2 : "invalid" };
                    return { done: true, get value() { state.terminalValues++; throw "terminal value"; } };
                }
            };
            Object.defineProperty(iterator, "return", {
                get() {
                    state.returnGets++; state.receivers = state.receivers && this === iterator;
                    if (kind === 0) throw "return getter";
                    if (kind === 2) return 1;
                    return function() {
                        state.returnCalls++; state.receivers = state.receivers && this === iterator;
                        if (kind === 1) throw "return call";
                        return kind === 3 ? 1 : {};
                    };
                }
            });
            const source: any = {
                [Symbol.iterator]() { state.receivers = state.receivers && this === source; return iterator; },
                return() { state.sourceReturns++; throw "wrong close target"; }
            };
            try {
                const sum = math.sumPrecise(source);
                console.log(kind, sum, state.returnGets, state.returnCalls, state.sourceReturns,
                    state.next, state.terminalValues, state.receivers);
            } catch (error: any) {
                console.log(kind, error instanceof TypeError, error.message === expectedMessage,
                    state.returnGets, state.returnCalls, state.sourceReturns, state.next, state.receivers);
            }
        }
        for (let kind = 0; kind < 6; kind++) check(kind);
        """;

    public const string CustomIteratorExpected = "0 true true 1 0 0 2 true\n"
        + "1 true true 1 1 0 2 true\n2 true true 1 0 0 2 true\n"
        + "3 true true 1 1 0 2 true\n4 true true 1 1 0 2 true\n"
        + "5 3 0 0 0 3 0 true\n";

    public const string ProtocolFailuresSource = """
        const math: any = Math;
        function check(kind: number) {
            const original = {};
            const state = { returnGets: 0, returnCalls: 0, steps: 0 };
            const iterator: any = {
                next() {
                    if (kind === 0) throw original;
                    if (state.steps++ > 0) return { done: true };
                    return {
                        get done() { if (kind === 1) throw original; return false; },
                        get value() { throw original; }
                    };
                }
            };
            Object.defineProperty(iterator, "return", {
                get() { state.returnGets++; return function() { state.returnCalls++; return {}; }; }
            });
            const source: any = { [Symbol.iterator]() { return iterator; } };
            try { math.sumPrecise(source); console.log("returned"); }
            catch (error) { console.log(kind, error === original, state.returnGets, state.returnCalls); }
        }
        for (let kind = 0; kind < 3; kind++) check(kind);
        """;

    public const string ProtocolFailuresExpected = "0 true 0 0\n1 true 0 0\n2 true 0 0\n";

    public const string YieldingFinallySource = """
        const math: any = Math;
        const state = { events: "", closes: 0 };
        function* values() {
            try {
                state.events += "invalid;";
                yield "invalid";
                state.events += "body-resumed;";
            } finally {
                state.closes++;
                state.events += "close;";
                yield 9;
                state.events += "finally-resumed;";
            }
        }
        const iterator: any = values();
        try { math.sumPrecise(iterator); console.log("returned"); }
        catch (error) { console.log(error instanceof TypeError, state.closes, state.events); }
        const completion = iterator.next();
        console.log(completion.done, completion.value === undefined, state.closes, state.events);
        """;

    public const string YieldingFinallyExpected = "true 1 invalid;close;\n"
        + "true true 1 invalid;close;finally-resumed;\n";

    public const string NonFiniteBeforeInvalidSource = """
        const math: any = Math;
        function check(label: string, first: number) {
            const state = { events: "", closes: 0 };
            function* values() {
                try {
                    state.events += "first;"; yield first;
                    state.events += "invalid;"; yield "invalid";
                    state.events += "resumed;";
                } finally {
                    state.closes++;
                    state.events += "close;";
                    throw "cleanup";
                }
            }
            try { math.sumPrecise(values()); console.log("returned"); }
            catch (error) { console.log(label, error instanceof TypeError, state.closes, state.events); }
        }
        check("NaN", NaN);
        check("Infinity", Infinity);
        check("-Infinity", -Infinity);
        """;

    public const string NonFiniteBeforeInvalidExpected = "NaN true 1 first;invalid;close;\n"
        + "Infinity true 1 first;invalid;close;\n-Infinity true 1 first;invalid;close;\n";

    public static IEnumerable<object[]> Cases()
    {
        yield return ["native-generator", NativeGeneratorSource, NativeGeneratorExpected];
        yield return ["custom-iterator", CustomIteratorSource, CustomIteratorExpected];
        yield return ["protocol-failures", ProtocolFailuresSource, ProtocolFailuresExpected];
        yield return ["yielding-finally", YieldingFinallySource, YieldingFinallyExpected];
        yield return ["non-finite-before-invalid", NonFiniteBeforeInvalidSource, NonFiniteBeforeInvalidExpected];
    }

    public static IEnumerable<object[]> ModeCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void NonNumberRejectionClosesOnlyTheActualIterator(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
