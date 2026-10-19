using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class NonCallableTaggedTemplateTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["exact-and-all-substitutions-before-type-error", """
            let calls = 0;
            const tag: any = 0;
            try { tag`x${++calls}`; }
            catch (error: any) {
                console.log(error instanceof TypeError, calls, typeof error, error.name);
            }
            const state = { order: "", coercions: 0 }, marker = {};
            const value: any = {
                [Symbol.toPrimitive]() { state.coercions++; throw marker; },
                toString() { state.coercions++; throw marker; }
            };
            function substitute(label: string): any { state.order += label; return value; }
            try { tag`a${substitute("A")}b${substitute("B")}c${substitute("C")}`; }
            catch (error: any) {
                console.log(error instanceof TypeError, state.order, state.coercions);
            }
            """, "true 1 object TypeError\ntrue ABC 0\n"];

        yield return ["sync-substitution-throw-wins-and-stops-later-values", """
            const marker = {}, state = { order: "", bases: 0, keys: 0, gets: 0, later: 0 };
            const owner: any = {
                get tag() { state.order += "G"; state.gets++; return 0; }
            };
            function base(): any { state.order += "B"; state.bases++; return owner; }
            function key(): string { state.order += "K"; state.keys++; return "tag"; }
            function first(): number { state.order += "A"; return 1; }
            function fail(): never { state.order += "X"; throw marker; }
            function later(): number { state.order += "L"; state.later++; return 2; }
            try { base()[key()]`a${first()}b${fail()}c${later()}`; }
            catch (error) {
                console.log(error === marker, state.order, state.bases, state.keys, state.gets, state.later);
            }
            """, "true BKGAX 1 1 1 0\n"];

        yield return ["async-substitutions-and-rejection-winner", """
            function record(state: any, label: string): number { state.order += label; return 1; }
            function fail(state: any): never { state.order += "X"; throw state.marker; }
            function later(state: any): number { state.later++; state.order += "L"; return 2; }
            async function typeFailure(state: any) {
                const tag: any = 0;
                return tag`a${await Promise.resolve(record(state, "A"))}b${record(state, "B")}`;
            }
            async function substitutionFailure(state: any) {
                const tag: any = 0;
                return tag`a${await Promise.resolve(record(state, "A"))}b${fail(state)}c${later(state)}`;
            }
            async function run() {
                const first = { order: "" }, second = { order: "", marker: {}, later: 0 };
                await typeFailure(first).then(
                    () => console.log("unexpected fulfillment"),
                    (error: any) => console.log(error instanceof TypeError, typeof error, error.name, first.order));
                await substitutionFailure(second).then(
                    () => console.log("unexpected fulfillment"),
                    (error: any) => console.log(error === second.marker, second.order, second.later));
            }
            run().catch((error: any) => console.log("unexpected", error.name, error.message));
            """, "true object TypeError AB\ntrue AX 0\n"];

        yield return ["pending-tag-reference-is-captured-once", """
            async function check(kind: number) {
                const state = { gets: 0, calls: 0, replacementCalls: 0, receiver: true };
                const owner: any = { marker: 7 };
                Object.defineProperty(owner, "tag", {
                    get() {
                        state.gets++;
                        if (kind === 0) return 0;
                        return function(this: any, strings: any, value: any) {
                            state.calls++; state.receiver = state.receiver && this === owner;
                            return this.marker + ":" + value;
                        };
                    }, configurable: true
                });
                let release: any;
                const gate = new Promise<any>(resolve => { release = resolve; });
                async function invoke() { return owner.tag`value:${await gate}`; }
                const task = invoke();
                console.log("paused", kind, state.gets, state.calls);
                Object.defineProperty(owner, "tag", {
                    value: kind === 0 ? function() { state.replacementCalls++; return "replacement"; } : 0,
                    configurable: true
                });
                release("X");
                await task.then(
                    (value: any) => console.log("value", kind, value, state.gets, state.calls,
                        state.replacementCalls, state.receiver),
                    (error: any) => console.log("error", kind, error instanceof TypeError,
                        state.gets, state.calls, state.replacementCalls));
            }
            async function run() { await check(0); await check(1); }
            run().catch((error: any) => console.log("unexpected", error.name, error.message));
            """, "paused 0 1 0\nerror 0 true 1 0 0\npaused 1 1 0\nvalue 1 7:X 1 1 0 true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void TaggedTemplatesEvaluateValuesBeforeCallabilityFailure(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, output) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            Assert.Equal(expected, output);
        }
        else
        {
            Assert.Equal(expected, TestHarness.Run(source, mode));
        }
    }
}
