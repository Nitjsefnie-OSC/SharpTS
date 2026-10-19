using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class PromiseSurplusArgumentsTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["exact-and-handler-positions", """
            const promise: any = Promise.resolve(1), exactState: any = { extra: 0 };
            function extra(): number { exactState.extra++; return 3; }
            try {
                promise.then(undefined, undefined, extra());
                console.log("accepted", exactState.extra);
            } catch (error: any) {
                console.log("rejected", exactState.extra, error.name, error.message);
            }
            async function run() {
                const value = {}, reason = {}, poison = {};
                const state = { fulfilled: 0, rejected: 0, evaluations: 0, calls: 0, thenGets: 0 };
                function ignored() { state.calls++; throw poison; }
                function evaluate(value: any): any { state.evaluations++; return value; }
                const thenable: any = { get then() { state.thenGets++; throw poison; } };
                function fulfill(item: any): any { state.fulfilled++; return item; }
                function reject(item: any): any { state.rejected++; return item; }
                const resolved: any = Promise.resolve(value), rejected: any = Promise.reject(reason);
                const first = resolved.then(fulfill, reject, evaluate(ignored), evaluate(thenable));
                const second = rejected.then(fulfill, reject, evaluate(ignored), evaluate(thenable));
                const results = await Promise.all([first, second]);
                console.log(results[0] === value, results[1] === reason, state.fulfilled,
                    state.rejected, state.evaluations, state.calls, state.thenGets);
            }
            run().catch((error: any) => console.log("unexpected", error.name, error.message));
            """, "accepted 1\ntrue true 1 1 4 0 0\n"];

        yield return ["optional-pass-through-and-lengths", """
            async function run() {
                const value = {}, reason = {}, poison = {};
                const state = { thenGets: 0, finallyCalls: 0, finallyArgs: 0 };
                const noncallable: any = { get then() { state.thenGets++; throw poison; } };
                const resolved: any = Promise.resolve(value), rejected: any = Promise.reject(reason);
                async function retainsReason(promise: any) {
                    try { await promise; return false; }
                    catch (error) { return error === reason; }
                }
                const cleanup = (...args: any[]) => {
                    state.finallyCalls++; state.finallyArgs += args.length; return "ignored";
                };
                const fulfilledJobs = [resolved.then(), resolved.catch(), resolved.finally(),
                    resolved.then(undefined, undefined, noncallable),
                    resolved.catch(noncallable, noncallable), resolved.finally(noncallable, noncallable)];
                const rejectedJobs = [retainsReason(rejected.then()), retainsReason(rejected.catch()),
                    retainsReason(rejected.finally()),
                    retainsReason(rejected.then(undefined, undefined, noncallable)),
                    retainsReason(rejected.catch(noncallable, noncallable)),
                    retainsReason(rejected.finally(noncallable, noncallable))];
                const fulfilledResults = await Promise.all(fulfilledJobs);
                const rejectedResults = await Promise.all(rejectedJobs);
                console.log(fulfilledResults.every(item => item === value),
                    rejectedResults.every(item => item === true), state.thenGets);
                const cleaned = await Promise.all([resolved.finally(cleanup, noncallable),
                    retainsReason(rejected.finally(cleanup, noncallable))]);
                console.log(cleaned[0] === value, cleaned[1], state.finallyCalls, state.finallyArgs, state.thenGets);
                console.log(Promise.prototype.then.length, Promise.prototype.catch.length, Promise.prototype.finally.length);
            }
            run().catch((error: any) => console.log("unexpected", error.name, error.message));
            """, "true true 0\ntrue true 2 0 0\n2 1 1\n"];

        yield return ["generic-catch-finally-invoke", """
            function check(kind: number) {
                const state = { order: "", gets: 0, calls: 0, correct: true };
                const handler = {}, result = {}, poison = {};
                const operation: any = kind === 0 ? Promise.prototype.catch : Promise.prototype.finally;
                const target: any = {
                    get then() {
                        state.order += "G"; state.gets++;
                        return function(this: any, first: any, second: any) {
                            state.order += "I"; state.calls++;
                            state.correct = state.correct && this === target && arguments.length === 2
                                && first === (kind === 0 ? undefined : handler) && second === handler;
                            return result;
                        };
                    }
                };
                function extra() { state.order += "X"; return function() { throw poison; }; }
                const actual = operation.call(target, handler, extra());
                console.log(kind, actual === result, state.gets, state.calls, state.correct, state.order);
            }
            check(0);
            check(1);
            """, "0 true 1 1 true XGI\n1 true 1 1 true XGI\n"];

        yield return ["abrupt-extras-species-and-brand-order", """
            const argumentMarker = {}, speciesMarker = {};
            const state = { order: "", poison: true, species: 0, handlers: 0 };
            class Source extends Promise<any> {
                static get [Symbol.species]() {
                    state.order += "species;"; state.species++;
                    if (state.poison) throw speciesMarker;
                    return Promise;
                }
            }
            function extra(throws: boolean): any {
                state.order += "extra;";
                if (throws) throw argumentMarker;
                return 3;
            }
            function handler(value: any): any {
                state.order += "handler;"; state.handlers++; return value;
            }
            async function run() {
                const source: any = new Source(resolve => resolve(1));
                try { source.then(handler, undefined, extra(true)); }
                catch (error) { console.log("argument", error === argumentMarker, state.order, state.species, state.handlers); }
                state.order = "";
                try { source.then(handler, undefined, extra(false)); }
                catch (error) { console.log("species", error === speciesMarker, state.order, state.species, state.handlers); }
                state.order = ""; state.poison = false;
                const result = source.then(handler, undefined, extra(false));
                console.log("sync", state.order, state.species, state.handlers);
                console.log("after", await result, state.order, state.species, state.handlers);
                const then: any = Promise.prototype.then;
                const invalid: any = { get constructor() { state.order += "constructor;"; throw speciesMarker; } };
                state.order = "";
                try { then.call(invalid, undefined, undefined, extra(true)); }
                catch (error) { console.log("brand-argument", error === argumentMarker, state.order); }
                state.order = "";
                try { then.call(invalid, undefined, undefined, extra(false)); }
                catch (error) { console.log("brand", error instanceof TypeError, state.order); }
            }
            run().catch((error: any) => console.log("unexpected", error.name, error.message));
            """, "argument true extra; 0 0\nspecies true extra;species; 1 0\n"
                + "sync extra;species; 2 0\nafter 1 extra;species;handler; 2 1\n"
                + "brand-argument true extra;\nbrand true extra;\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ReactionMethodsEvaluateAndIgnoreSurplusArguments(
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
