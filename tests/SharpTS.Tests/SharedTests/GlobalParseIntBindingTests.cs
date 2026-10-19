using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class GlobalParseIntBindingTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", """
            const original = globalThis.parseInt;
            const replacement = function (_text: string, _radix?: number): number { return null as any; };
            try {
                globalThis.parseInt = replacement;
                console.log(parseInt === replacement, globalThis.parseInt === replacement,
                    parseInt("7", 10), globalThis.parseInt("7", 10));
            } finally { globalThis.parseInt = original; }
            """, "true true null null\n"];
        yield return ["dot-bracket-writes-and-captured-values", """
            const target: any = globalThis;
            const original = target.parseInt;
            const first = (_text: string, _radix?: number) => 17;
            const second = (_text: string, _radix?: number) => 21;
            try {
                target.parseInt = first;
                const held = parseInt;
                console.log(parseInt === first, target.parseInt === first, parseInt("7", 10), held("7", 10));
                target["parseInt"] = second;
                console.log(parseInt === second, parseInt("7", 10), held("7", 10));
            } finally { target.parseInt = original; }
            console.log(parseInt("12", 10));
            """, "true true 17 17\ntrue 21 17\n12\n"];
        yield return ["defineproperty-data-write-and-identity", """
            const target: any = globalThis;
            const original = Object.getOwnPropertyDescriptor(target, "parseInt")!;
            const replacement = (_text: string, _radix?: number) => 31;
            try {
                Object.defineProperty(target, "parseInt", { value: replacement, writable: true, configurable: true });
                const held = parseInt;
                console.log(parseInt === replacement, held === replacement, parseInt("7", 10), held("8", 10));
            } finally { Object.defineProperty(target, "parseInt", original); }
            console.log(parseInt("12", 10));
            """, "true true 31 31\n12\n"];
        yield return ["local-parameter-let-and-captured-shadows", """
            const target: any = globalThis, original = target.parseInt;
            function parameter(parseInt: any) { return parseInt("7", 10); }
            function local() {
                let parseInt = (_text: string, _radix?: number) => 61;
                function captured() { return parseInt("7", 10); }
                return captured();
            }
            async function asynchronous(parseInt: any) { await Promise.resolve(0); return parseInt("7", 10); }
            async function run() {
                try {
                    target.parseInt = () => 41;
                    console.log(parameter(() => 51), local(), parseInt("7", 10));
                    console.log(await asynchronous(() => 71), parseInt("7", 10));
                } finally { target.parseInt = original; }
            }
            run().catch((error: any) => console.log("unexpected", error && error.name));
            """, "51 61 41\n71 41\n"];
        yield return ["accessor-callee-read-before-arguments", """
            const target: any = globalThis;
            const original = Object.getOwnPropertyDescriptor(target, "parseInt")!;
            const events: string[] = [];
            let result: any;
            try {
                Object.defineProperty(target, "parseInt", {
                    configurable: true,
                    get() {
                        events.push("get:" + (this === target));
                        return function(_text: string, _radix?: number) { events.push("call"); return 17; };
                    }
                });
                function first() {
                    events.push("first");
                    Object.defineProperty(target, "parseInt", { value: () => 99, writable: true, configurable: true });
                    return "7";
                }
                function second() { events.push("second"); return 10; }
                result = parseInt(first(), second());
            } finally { Object.defineProperty(target, "parseInt", original); }
            console.log(result, events.join(","));
            """, "17 get:true,first,second,call\n"];
        yield return ["async-accessor-callee-read-before-awaited-argument", """
            const target: any = globalThis;
            const original = Object.getOwnPropertyDescriptor(target, "parseInt")!;
            const events: string[] = [];
            async function run() {
                let result: any;
                try {
                    Object.defineProperty(target, "parseInt", {
                        configurable: true,
                        get() {
                            events.push("get:" + (this === target));
                            return function(_text: string, _radix?: number) { events.push("call"); return 17; };
                        }
                    });
                    async function argument() {
                        events.push("argument");
                        await Promise.resolve(0);
                        Object.defineProperty(target, "parseInt", { value: () => 99, writable: true, configurable: true });
                        return "7";
                    }
                    result = parseInt(await argument(), 10);
                } finally { Object.defineProperty(target, "parseInt", original); }
                console.log(result, events.join(","));
            }
            run().catch((error: any) => console.log("unexpected", error && error.name));
            """, "17 get:true,argument,call\n"];
        yield return ["throwing-accessor-suppresses-arguments", """
            const target: any = globalThis;
            const original = Object.getOwnPropertyDescriptor(target, "parseInt")!;
            const token = {};
            let calls = 0, same = false;
            try {
                Object.defineProperty(target, "parseInt", { configurable: true, get() { throw token; } });
                function argument() { calls++; return "7"; }
                try { parseInt(argument(), 10); }
                catch (error) { same = error === token; }
            } finally { Object.defineProperty(target, "parseInt", original); }
            console.log(same, calls);
            """, "true 0\n"];
        yield return ["noncallable-and-nullish-optional-call", """
            const target: any = globalThis, original = target.parseInt;
            let calls = 0;
            function argument() { calls++; return "7"; }
            try {
                target.parseInt = 5;
                try { parseInt(argument(), 10); }
                catch (error) { console.log(error instanceof TypeError, calls); }
                calls = 0;
                try { parseInt?.(argument(), 10); }
                catch (error) { console.log(error instanceof TypeError, calls); }
                calls = 0;
                target.parseInt = null;
                console.log(parseInt?.(argument(), 10) === undefined, calls);
            } finally { target.parseInt = original; }
            """, "true 1\ntrue 1\ntrue 0\n"];
        yield return ["canonical-with-number-alias", """
            const original = globalThis.parseInt, bare = parseInt;
            console.log(original === bare, Number.parseInt === bare);
            try {
                globalThis.parseInt = (_text: string, _radix?: number) => 17;
                console.log(parseInt === globalThis.parseInt, parseInt("7", 10));
            } finally { globalThis.parseInt = original; }
            console.log(parseInt === bare, globalThis.parseInt === bare, Number.parseInt === bare, parseInt("12", 10));
            """, "true true\ntrue 17\ntrue true true 12\n"];
        yield return ["delete-and-restore-global-function", """
            const target: any = globalThis;
            const original = Object.getOwnPropertyDescriptor(target, "parseInt")!;
            try {
                console.log(delete target.parseInt, typeof parseInt);
                try { const value = parseInt; console.log("present", typeof value); }
                catch (error) { console.log(error instanceof ReferenceError); }
            } finally { Object.defineProperty(target, "parseInt", original); }
            console.log(parseInt === Number.parseInt, parseInt("12", 10));
            """, "true undefined\ntrue\ntrue 12\n"];
        yield return ["strict-bare-and-detached-receiver", """
            const target: any = globalThis, original = target.parseInt;
            try {
                target.parseInt = function(_text: string, _radix?: number): any { "use strict"; return this; };
                const held = parseInt;
                console.log((parseInt("7", 10) as any) === undefined, (held("8", 10) as any) === undefined);
            } finally { target.parseInt = original; }
            """, "true true\n"];
    }

    public static IEnumerable<object[]> ModeCases()
    {
        foreach (var row in Cases())
        {
            yield return [row[0], row[1], row[2], ExecutionMode.Interpreted];
            // Qualified compiled call is #2539; descriptor-defined bare calls are #2693.
            // A deleted global's compiled bare read returns undefined instead of throwing (#2696).
            if ((string)row[0] is "dot-bracket-writes-and-captured-values" or
                "local-parameter-let-and-captured-shadows" or "noncallable-and-nullish-optional-call" or
                "canonical-with-number-alias" or "strict-bare-and-detached-receiver")
                yield return [row[0], row[1], row[2], ExecutionMode.Compiled];
        }
    }

    [Theory, MemberData(nameof(ModeCases))]
    public void GlobalWritesAndBareReadsShareTheLiveBinding(
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
