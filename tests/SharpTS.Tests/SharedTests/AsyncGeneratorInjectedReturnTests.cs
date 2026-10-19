using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class AsyncGeneratorInjectedReturnTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", """
            async function* values(): any {
                try { yield 1; } finally { console.log("finally"); }
            }
            async function run() {
                const marker = { tag: "caller" };
                const token: any = {};
                const iterator: any = values();
                console.log("first", (await iterator.next()).value, marker.tag);
                const input: any = undefined;
                try {
                    const result: any = await iterator.return(input);
                    console.log("resolved", marker.tag, result.done, result.value === input, result.value === 7);
                } catch (error: any) {
                    console.log("rejected", marker.tag, error === token, error === input, error === undefined);
                }
                console.log("after", marker.tag, (await iterator.next()).done);
            }
            run().catch((error: any) => console.log("unexpected", typeof error, error && error.message));
            """, "first 1 caller\nfinally\nresolved caller true true false\nafter caller true\n"];
        yield return ["primitive-return-bypasses-catch", """
            let caught = 0;
            async function* values(): any {
                try { yield 1; }
                catch (error) { caught++; console.log("caught"); yield 99; }
                finally { console.log("finally"); }
                console.log("after-body");
            }
            async function run() {
                const iterator: any = values();
                console.log((await iterator.next()).value);
                const result: any = await iterator.return(7);
                console.log(result.value, result.done, caught);
                console.log((await iterator.next()).done, caught);
            }
            run().catch((error: any) => console.log("unexpected", error && error.name));
            """, "1\nfinally\n7 true 0\ntrue 0\n"];
        yield return ["return-from-injected-catch", """
            const token = {};
            async function* values(): any {
                try { yield 1; }
                catch (error) {
                    console.log(error === token);
                    yield 2;
                    console.log("after-catch");
                } finally { console.log("finally"); }
                yield 99;
            }
            async function run() {
                const iterator: any = values();
                console.log((await iterator.next()).value);
                const handled: any = await iterator.throw(token);
                console.log(handled.value, handled.done);
                const result: any = await iterator.return(7);
                console.log(result.value, result.done);
                console.log((await iterator.next()).done);
            }
            run().catch((error: any) => console.log("unexpected", error && error.name));
            """, "1\ntrue\n2 false\nfinally\n7 true\ntrue\n"];
        yield return ["finally-return-and-throw-override", """
            const token = {};
            async function* returning(): any {
                try { yield 1; } finally { return 9; }
            }
            async function* throwing(): any {
                try { yield 1; } finally { throw token; }
            }
            async function run() {
                const first: any = returning();
                await first.next();
                const result: any = await first.return(7);
                console.log(result.value, result.done);
                const second: any = throwing();
                await second.next();
                try { await second.return(7); }
                catch (error) { console.log(error === token); }
                console.log((await second.next()).done);
            }
            run().catch((error: any) => console.log("unexpected", error && error.name));
            """, "9 true\ntrue\ntrue\n"];
        yield return ["nested-finally-order", """
            const events: string[] = [];
            async function* values(): any {
                try {
                    try { yield 1; }
                    finally { events.push("inner"); }
                } finally { events.push("outer"); }
                events.push("after");
            }
            async function run() {
                const iterator: any = values();
                await iterator.next();
                const result: any = await iterator.return(7);
                console.log(result.value, result.done, events.join(","));
                console.log((await iterator.next()).done, events.join(","));
            }
            run().catch((error: any) => console.log("unexpected", error && error.name));
            """, "7 true inner,outer\ntrue inner,outer\n"];
        yield return ["yielding-finally-resumes-original-return", """
            async function* values(): any {
                try {
                    try { yield 1; }
                    finally {
                        const sent: any = yield 2;
                        console.log("resumed", sent);
                    }
                } finally { console.log("outer"); }
                yield 99;
            }
            async function run() {
                const iterator: any = values();
                console.log((await iterator.next()).value);
                const suspended: any = await iterator.return(7);
                console.log(suspended.value, suspended.done);
                const result: any = await iterator.next("sent");
                console.log(result.value, result.done);
                console.log((await iterator.next()).done);
            }
            run().catch((error: any) => console.log("unexpected", error && error.name));
            """, "1\n2 false\nresumed sent\nouter\n7 true\ntrue\n"];
        yield return ["awaiting-finally-preserves-caller-locals", """
            async function* values(): any {
                const marker = { tag: "generator" };
                try { yield 1; }
                finally {
                    console.log("before", marker.tag);
                    const value = await Promise.resolve(3);
                    console.log("after", marker.tag, value);
                }
            }
            async function run() {
                const marker = { tag: "caller" };
                const iterator: any = values();
                console.log((await iterator.next()).value, marker.tag);
                const result: any = await iterator.return(7);
                console.log(result.value, result.done, marker.tag);
                console.log((await iterator.next()).done, marker.tag);
            }
            run().catch((error: any) => console.log("unexpected", error && error.name));
            """, "1 caller\nbefore generator\nafter generator 3\n7 true caller\ntrue caller\n"];
        yield return ["second-return-overrides-yielding-finally", """
            const events: string[] = [];
            async function* values(): any {
                try {
                    try { yield 1; }
                    finally { yield 2; events.push("after-yield"); }
                } finally { events.push("outer"); }
            }
            async function run() {
                const iterator: any = values();
                await iterator.next();
                const suspended: any = await iterator.return(7);
                console.log(suspended.value, suspended.done);
                const result: any = await iterator.return(9);
                console.log(result.value, result.done, events.join(","));
                console.log((await iterator.next()).done, events.join(","));
            }
            run().catch((error: any) => console.log("unexpected", error && error.name));
            """, "2 false\n9 true outer\ntrue outer\n"];
        yield return ["no-finally-and-not-started-controls", """
            async function* values(): any { yield 1; yield 2; }
            async function run() {
                const first: any = values();
                await first.next();
                const absent: any = await first.return(undefined);
                console.log(absent.value === undefined, absent.done, (await first.next()).done);
                const second: any = values();
                await second.next();
                const present: any = await second.return(7);
                console.log(present.value, present.done, (await second.next()).done);
                const fresh: any = values();
                const unopened: any = await fresh.return(7);
                console.log(unopened.value, unopened.done, (await fresh.next()).done);
            }
            run().catch((error: any) => console.log("unexpected", error && error.name));
            """, "true true true\n7 true true\n7 true true\n"];
        yield return ["normal-next-and-injected-throw-controls", """
            const token = {};
            async function* values(): any {
                try {
                    const sent: any = yield 1;
                    yield sent;
                } catch (error) {
                    console.log(error === token);
                    yield 8;
                } finally { console.log("finally"); }
                return 9;
            }
            async function run() {
                const normal: any = values();
                console.log((await normal.next()).value);
                console.log((await normal.next(7)).value);
                const normalEnd: any = await normal.next();
                console.log(normalEnd.value, normalEnd.done);
                const throwing: any = values();
                await throwing.next();
                const handled: any = await throwing.throw(token);
                console.log(handled.value, handled.done);
                const end: any = await throwing.next();
                console.log(end.value, end.done);
            }
            run().catch((error: any) => console.log("unexpected", error && error.name));
            """, "1\n7\nfinally\n9 true\ntrue\n8 false\nfinally\n9 true\n"];
    }

    public static IEnumerable<object[]> ModeCases()
    {
        foreach (var row in Cases())
        {
            yield return [row[0], row[1], row[2], ExecutionMode.Interpreted];
            // #2688: verified compiler outputs mishandle richer injected-return cleanup.
            if ((string)row[0] is "issue" or "primitive-return-bypasses-catch" or
                "awaiting-finally-preserves-caller-locals" or "no-finally-and-not-started-controls" or
                "normal-next-and-injected-throw-controls")
                yield return [row[0], row[1], row[2], ExecutionMode.Compiled];
        }
    }

    [Theory, MemberData(nameof(ModeCases))]
    public void InjectedReturnsRunFinallyAndPreserveThePendingCompletion(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, output) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            Assert.Equal(expected, output);
        }
        else Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
