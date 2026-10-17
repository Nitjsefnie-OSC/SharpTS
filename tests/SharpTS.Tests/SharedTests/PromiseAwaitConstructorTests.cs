using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class PromiseAwaitConstructorTests
{
    public const string IssueSource = """
        let reads = 0;
        class P extends Promise<number> {
            static get [Symbol.species]() { reads++; return this as any; }
        }
        async function main() {
            const q = P.resolve(3).then(v => v + 1);
            const before = reads;
            const value = await q;
            console.log(before, reads, value);
        }
        main();
        """;

    public const string LiveThenOrderSource = """
        const state: any = { events: [], gets: 0, calls: 0, receiver: false, nativeGets: 0 };
        class P extends Promise<any> {}
        const source: any = P.resolve(3);
        Object.defineProperty(source, "then", {
            configurable: true,
            get() {
                state.gets++; state.events.push("get");
                return function(this: any, resolve: any, reject: any) {
                    state.calls++; state.receiver = this === source; state.events.push("then");
                    resolve(9); reject("late"); throw "late";
                };
            }
        });
        const native: any = Promise.resolve(4);
        Object.defineProperty(native, "then", { get() { state.nativeGets++; throw "unused"; } });
        async function main() {
            state.events.push("before");
            const value = await source;
            state.events.push("after");
            console.log(value, state.gets, state.calls, state.receiver, state.events.join(","));
            console.log("intrinsic", await native, state.nativeGets);
        }
        const pending = main();
        state.events.push("caller");
        pending.then(() => {}, (error: any) => console.log("unexpected", error));
        """;

    public const string IntrinsicIdentitySource = """
        const intrinsic = Promise, marker = {};
        const state = { constructors: 0, thenReads: 0 };
        class P extends Promise<any> {}
        const base: any = intrinsic.resolve(4), subclass: any = P.resolve(5);
        for (const value of [base, subclass]) {
            Object.defineProperty(value, "constructor", {
                get() { state.constructors++; return intrinsic; }, configurable: true
            });
            Object.defineProperty(value, "then", {
                get() { state.thenReads++; throw marker; }, configurable: true
            });
        }
        async function run(Promise: any) {
            console.log(await base, await subclass, state.constructors, state.thenReads);
        }
        run({ resolve() { throw marker; } }).then(() => console.log("done"), error => console.log("unexpected", error));
        """;

    public const string PendingCaptureSource = """
        const state: any = { events: [], constructors: 0, gets: 0, calls: 0, replacements: 0, same: false };
        const source: any = Promise.resolve(1), other = {}, marker = {};
        Object.defineProperty(source, "constructor", {
            get() { state.constructors++; state.events.push("constructor"); return other; }, configurable: true
        });
        Object.defineProperty(source, "then", {
            get() {
                state.gets++; state.events.push("get");
                return function(this: any, resolve: any, reject: any) {
                    state.calls++; state.same = this === source; state.events.push("then");
                    setTimeout(() => { state.events.push("timer"); resolve(9); reject(marker); }, 1);
                };
            }, configurable: true
        });
        async function run() {
            state.events.push("before");
            const value = await source;
            state.events.push("after");
            console.log(value, state.constructors, state.gets, state.calls, state.replacements, state.same,
                state.events.join(","));
        }
        const work = run();
        state.events.push("caller");
        Object.defineProperty(source, "then", { value() { state.replacements++; }, configurable: true });
        work.then(() => console.log("done"), error => console.log("unexpected", error));
        """;

    public const string RejectionSource = """
        class P extends Promise<any> {}
        const constructorMarker = {}, getterMarker = {}, rejectionMarker = {}, late = {};
        const state = { constructors: 0, unusedThen: 0, getters: 0, calls: 0, same: false };
        const constructorThrows: any = P.resolve(0);
        Object.defineProperty(constructorThrows, "constructor", {
            get() { state.constructors++; throw constructorMarker; }, configurable: true
        });
        Object.defineProperty(constructorThrows, "then", { get() { state.unusedThen++; throw late; } });
        const getterThrows: any = P.resolve(0);
        Object.defineProperty(getterThrows, "then", {
            get() { state.getters++; throw getterMarker; }, configurable: true
        });
        const rejects: any = P.resolve(0);
        Object.defineProperty(rejects, "then", {
            value: function(this: any, resolve: any, reject: any) {
                state.calls++; state.same = this === rejects;
                setTimeout(() => { reject(rejectionMarker); resolve(99); reject(late); }, 1);
            }, configurable: true
        });
        const noncallable: any = P.resolve(0);
        Object.defineProperty(noncallable, "then", { value: 7, configurable: true });
        async function run() {
            try { await constructorThrows; }
            catch (error) { console.log("constructor", error === constructorMarker, state.constructors, state.unusedThen); }
            try { await getterThrows; }
            catch (error) { console.log("getter", error === getterMarker, state.getters); }
            try { await rejects; }
            catch (error) { console.log("pending", error === rejectionMarker, state.calls, state.same); }
            console.log("noncallable", await noncallable === noncallable, noncallable.then);
        }
        run().then(() => console.log("done"), error => console.log("unexpected", error));
        """;

    // #2434: an explicit arrow return type avoids the independent inferred-void
    // ordinary-return bug while preserving this pending async-arrow await path.
    public const string ArrowSource = """
        class P extends Promise<any> {}
        function start(label: string) {
            const source: any = P.resolve(0), state = { calls: 0, same: false };
            Object.defineProperty(source, "then", {
                value: function(this: any, resolve: any, reject: any) {
                    state.calls++; state.same = this === source;
                    setTimeout(() => resolve(7), 1);
                }, configurable: true
            });
            const read = async (): Promise<void> => {
                const value = await source;
                console.log(label, value, state.calls, state.same);
            };
            const work = read();
            console.log("paused", state.calls);
            return work;
        }
        start("arrow").then(() => console.log("done"), error => console.log("unexpected", error));
        """;

    public const string GeneratorSource = """
        class P extends Promise<any> {}
        const source: any = P.resolve(0), state = { calls: 0, same: false };
        Object.defineProperty(source, "then", {
            value: function(this: any, resolve: any, reject: any) {
                state.calls++; state.same = this === source;
                setTimeout(() => resolve(8), 1);
            }, configurable: true
        });
        async function* values(): AsyncGenerator<any, void, unknown> {
            yield "pause";
            const value = await source;
            yield value;
        }
        async function run() {
            const iterator = values();
            console.log((await iterator.next()).value);
            const work = iterator.next();
            console.log("queued", state.calls);
            const result = await work;
            console.log(result.value, result.done, state.calls, state.same);
            console.log((await iterator.next()).done);
        }
        run().then(() => console.log("done"), error => console.log("unexpected", error));
        """;

    public const string AsyncFromSyncSource = """
        class P extends Promise<any> {}
        const source: any = P.resolve(0), state = { calls: 0, same: false };
        Object.defineProperty(source, "then", {
            value: function(this: any, resolve: any, reject: any) {
                state.calls++; state.same = this === source;
                setTimeout(() => resolve(9), 1);
            }, configurable: true
        });
        async function run() {
            for await (const value of [source]) console.log(value, state.calls, state.same);
        }
        const work = run();
        console.log("paused", state.calls);
        work.then(() => console.log("done"), error => console.log("unexpected", error));
        """;

    public const string OwnAccessorReceiverSource = """
        const intrinsic = Promise, other = {}, marker = {};
        const state = { constructors: 0, constructorThis: false, unusedThen: 0,
            thenGets: 0, thenGetThis: false, thenCalls: 0, thenCallThis: false,
            setterCalls: 0, setterConstructorThenCalls: 0 };
        class P extends Promise<any> {}
        const direct: any = P.resolve(4);
        Object.defineProperty(direct, "constructor", {
            get: function(this: any) {
                state.constructors++; state.constructorThis = this === direct;
                return this === direct ? intrinsic : other;
            }, configurable: true
        });
        Object.defineProperty(direct, "then", {
            get() { state.unusedThen++; throw marker; }, configurable: true
        });
        const live: any = intrinsic.resolve(1);
        Object.defineProperty(live, "constructor", { value: other, configurable: true });
        Object.defineProperty(live, "then", {
            get: function(this: any) {
                state.thenGets++; state.thenGetThis = this === live;
                return function(this: any, resolve: any, reject: any) {
                    state.thenCalls++; state.thenCallThis = this === live; resolve(8);
                };
            }, configurable: true
        });
        const setterConstructor: any = intrinsic.resolve(3);
        Object.defineProperty(setterConstructor, "constructor", {
            set(value: any) { state.setterCalls++; }, configurable: true
        });
        Object.defineProperty(setterConstructor, "then", {
            value(resolve: any, reject: any) { state.setterConstructorThenCalls++; resolve(9); }, configurable: true
        });
        const setterThen: any = intrinsic.resolve(4);
        Object.defineProperty(setterThen, "constructor", { value: other, configurable: true });
        Object.defineProperty(setterThen, "then", {
            set(value: any) { state.setterCalls++; }, configurable: true
        });
        async function run() {
            console.log("fast", await direct, state.constructors, state.constructorThis, state.unusedThen);
            console.log("live", await live, state.thenGets, state.thenGetThis, state.thenCalls, state.thenCallThis);
            console.log("setter-constructor", await setterConstructor, state.setterConstructorThenCalls, state.setterCalls);
            console.log("setter-then", await setterThen === setterThen, state.setterCalls);
        }
        run().then(() => console.log("done"), error => console.log("unexpected", error === marker));
        """;

    public const string ImplicitIteratorNextAndReturnSource = """
        class P extends Promise<any> {}
        const state = { calls: 0, same: true };
        const nextPromise: any = P.resolve({ done: false, value: 0 });
        const returnPromise: any = P.resolve({ done: true, value: 0 });
        Object.defineProperty(nextPromise, 'then', {
            value: function(this: any, resolve: any) {
                state.calls++; state.same = state.same && this === nextPromise;
                resolve({ done: false, value: 7 });
            }, configurable: true
        });
        Object.defineProperty(returnPromise, 'then', {
            value: function(this: any, resolve: any) {
                state.calls++; state.same = state.same && this === returnPromise;
                resolve({ done: true, value: 9 });
            }, configurable: true
        });
        const iterator: any = {
            next() { return nextPromise; },
            return() { return returnPromise; },
            [Symbol.asyncIterator]() { return this; }
        };
        async function run() {
            for await (const value of iterator) {
                console.log(value, state.calls, state.same);
                break;
            }
            console.log('closed', state.calls, state.same);
        }
        run().then(() => {}, (error: any) => console.log('unexpected', error));
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "1 2 4\n"];
        yield return ["live-then-order", LiveThenOrderSource, "9 1 1 true before,get,caller,then,after\nintrinsic 4 0\n"];
        yield return ["intrinsic-constructor-identity", IntrinsicIdentitySource, "4 5 2 0\ndone\n"];
        yield return ["changed-intrinsic-pending-capture", PendingCaptureSource, "9 1 1 1 0 true before,constructor,get,caller,then,timer,after\ndone\n"];
        yield return ["rejection-and-abrupt-getters", RejectionSource, "constructor true 1 0\ngetter true 1\npending true 1 true\nnoncallable true 7\ndone\n"];
        yield return ["pending-async-arrow", ArrowSource, "paused 0\narrow 7 1 true\ndone\n"];
        yield return ["async-generator-after-yield", GeneratorSource, "pause\nqueued 0\n8 false 1 true\ntrue\ndone\n"];
        yield return ["async-from-sync-value", AsyncFromSyncSource, "paused 0\n9 1 true\ndone\n"];
        yield return ["promise-own-accessor-receiver", OwnAccessorReceiverSource, "fast 4 1 true 0\nlive 8 1 true 1 true\nsetter-constructor 9 1 0\nsetter-then true 0\ndone\n"];
        yield return ["implicit-async-iterator-next-and-return", ImplicitIteratorNextAndReturnSource, "7 1 true\nclosed 2 true\n"];
    }

    public static IEnumerable<object[]> ModeCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { mode, row[0], row[1], row[2] }));

    [Theory, MemberData(nameof(ModeCases))]
    public void AwaitUsesLivePromiseConstructorIdentityAndAssimilatesOtherPromises(
        ExecutionMode mode, string name, string source, string expected)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
