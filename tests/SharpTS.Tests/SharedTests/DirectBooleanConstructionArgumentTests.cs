using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class DirectBooleanConstructionArgumentTests
{
    // Preserve the exact issue source, including its two terminal LF characters.
    public const string OriginalSource = """
        function probe(Boolean: any) {
            let calls = 0;
            const value: any = new Boolean('', calls++);
            console.log(value.valueOf(), calls);
        }
        probe(Boolean);
        """ + "\n\n";

    public const string OrderAndAbruptSource = """
        function probe(Boolean: any) {
            const state = { trace: "" };
            function mark(label: string, value: any): any { state.trace += label; return value; }
            const value: any = new Boolean(mark("a", ""), mark("b", 1), mark("c", 2));
            console.log(value.valueOf(), state.trace);
            state.trace = "";
            const marker = {};
            function fail(): never { state.trace += "b"; throw marker; }
            try { new Boolean(mark("a", ""), fail(), mark("c", 2)); console.log("unreachable"); }
            catch (error) { console.log(error === marker, state.trace); }
        }
        probe(Boolean);
        """ + "\n";

    public const string SpreadSource = """
        function probe(Boolean: any) {
            const state = { trace: "" };
            function* values(first: any): Generator<any, void, unknown> {
                state.trace += "a"; yield first; state.trace += "b"; yield 2; state.trace += "c";
            }
            console.log((new Boolean("", ...values(1)) as any).valueOf(), state.trace);
            state.trace = "";
            console.log((new Boolean(...values("")) as any).valueOf(), state.trace);
            console.log((new Boolean(...[], false) as any).valueOf());
            state.trace = "";
            const marker = {};
            function* broken(): Generator<number, void, unknown> {
                state.trace += "a"; yield 0; state.trace += "b"; throw marker;
            }
            function last(): number { state.trace += "c"; return 1; }
            try { new Boolean(...broken(), last()); console.log("unreachable"); }
            catch (error) { console.log(error === marker, state.trace); }
        }
        probe(Boolean);
        """ + "\n";

    public const string PendingSource = """
        function pending(label: string, value: any, state: { trace: string }): Promise<any> {
            return new Promise(resolve => setTimeout(() => { state.trace += label; resolve(value); }, 0));
        }
        async function probe(Boolean: any) {
            const state = { trace: "" };
            const value: any = new Boolean(await pending("a", "", state), await pending("b", 1, state), await pending("c", 2, state));
            console.log(value.valueOf(), state.trace);
        }
        probe(Boolean).then(() => console.log("done"), error => console.log("unexpected", error));
        """ + "\n";

    public const string YieldSource = """
        function* probe(Boolean: any): Generator<any, void, any> {
            const value: any = new Boolean(yield "first", yield "second");
            console.log(value.valueOf());
        }
        const iterator = probe(Boolean);
        console.log(iterator.next().value);
        console.log(iterator.next("").value);
        console.log(iterator.next(2).done);
        """ + "\n";

    public const string ControlsSource = """
        function probe(Boolean: any) {
            console.log(new Boolean().valueOf(), new Boolean(false).valueOf(), new Boolean(0).valueOf(),
                new Boolean("").valueOf(), new Boolean(null).valueOf(), new Boolean(undefined).valueOf(),
                new Boolean("x").valueOf(), new Boolean({}).valueOf());
            const first: any = new Boolean(""), second: any = new Boolean("");
            console.log(typeof first, first !== second, Object.getPrototypeOf(first) === Boolean.prototype,
                first instanceof Boolean, first instanceof Object);
            let calls = 0;
            const input = {
                valueOf() { calls += 100; throw "valueOf"; },
                toString() { calls += 1000; throw "toString"; },
                [Symbol.toPrimitive]() { calls += 10000; throw "toPrimitive"; }
            };
            console.log((new Boolean(input, calls++) as any).valueOf(), calls);
        }
        probe(Boolean);
        const Alias: any = Boolean;
        const Bound: any = (Boolean as any).bind(null, "");
        let calls = 0;
        const aliased: any = new Alias("", calls++), bound: any = new Bound(calls++);
        console.log(aliased.valueOf(), bound.valueOf(), calls);
        """ + "\n";

    // Keep the typed generator protocol without the independent #2255 declaration-return bug.
    public const string AsyncFamiliesSource = """
        function pending(label: string, value: any, state: { trace: string }): Promise<any> {
            return new Promise(resolve => setTimeout(() => { state.trace += label; resolve(value); }, 0));
        }
        function start(Boolean: any): Promise<void> {
            const state = { trace: "" };
            const arrow = async (): Promise<void> => {
                const value: any = new Boolean(await pending("a", "", state), await pending("b", 2, state));
                console.log("arrow", value.valueOf(), state.trace);
            };
            return arrow();
        }
        const values: (Boolean: any) => AsyncGenerator<string, boolean, string> = async function* (Boolean: any) {
            const state = { trace: "" };
            const first = yield "first";
            const value: any = new Boolean(first, await pending("a", 1, state), await pending("b", 2, state));
            console.log("generator", state.trace);
            return value.valueOf();
        };
        async function run(Boolean: any): Promise<void> {
            await start(Boolean);
            const iterator = values(Boolean);
            console.log((await iterator.next()).value);
            const result = await iterator.next("");
            console.log("async-generator", result.value, result.done);
        }
        run(Boolean).then(() => console.log("done"), error => console.log("unexpected", error));
        """ + "\n";

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue-direct-boolean-surplus", OriginalSource, "false 1\n"];
        yield return ["ordinary-surplus-order-and-abrupt-completion", OrderAndAbruptSource, "false abc\ntrue ab\n"];
        yield return ["leading-surplus-empty-and-abrupt-spreads", SpreadSource, "false abc\nfalse abc\nfalse\ntrue ab\n"];
        yield return ["pending-first-and-surplus-arguments", PendingSource, "false abc\ndone\n"];
        yield return ["yielding-first-and-surplus-arguments", YieldSource, "first\nsecond\nfalse\ntrue\n"];
        yield return ["truthiness-identity-hooks-and-constructor-values", ControlsSource, "false false false false false false true true\nobject true true true true\ntrue 1\nfalse false 2\n"];
        yield return ["async-arrow-and-async-generator-arguments", AsyncFamiliesSource, "arrow false ab\nfirst\ngenerator ab\nasync-generator false true\ndone\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void DirectBooleanConstructionEvaluatesEveryArgumentBeforeCreatingTheWrapper(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
