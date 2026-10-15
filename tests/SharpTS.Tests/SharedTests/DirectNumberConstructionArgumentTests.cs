using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class DirectNumberConstructionArgumentTests
{
    public const string IssueSource = """
        function probe(Number: any) {
            let calls = 0;
            const value: any = new Number(7, calls++);
            console.log(value.valueOf(), calls);
        }
        probe(Number);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", IssueSource, "7 1\n"];
        yield return ["arguments-before-conversion", """
            function probe(Number: any) {
                let trace = "";
                const input: any = { value: 3, valueOf() { trace += "v"; return this.value; } };
                let selected: any = input;
                function first(): any { trace += "a"; return selected; }
                function second(): any { trace += "b"; input.value = 13; selected = {}; return 99; }
                function third(): any { trace += "c"; return null; }
                const value: any = new Number(first(), second(), third());
                console.log(trace, value.valueOf(), input.value, selected !== input);
            }
            probe(Number);
            """, "abcv 13 13 true\n"];
        yield return ["abrupt-arguments-and-conversion", """
            function probe(Number: any) {
                let trace = "";
                const token: any = {};
                const input: any = { valueOf() { trace += "v"; return 7; } };
                function first(): any { trace += "a"; return input; }
                function fail(): any { trace += "x"; throw token; }
                try { new Number(first(), fail(), trace += "z"); }
                catch (error) { console.log(trace, error === token); }
                trace = "";
                let effects = 0;
                try { new Number(Symbol("invalid"), ++effects, trace += "s"); }
                catch (error) { console.log(error instanceof TypeError, effects, trace); }
                trace = "";
                const throwing: any = { valueOf() { trace += "v"; throw token; } };
                try { new Number((trace += "a", throwing), trace += "b"); }
                catch (error) { console.log(trace, error === token); }
            }
            probe(Number);
            """, "ax true\ntrue 1 s\nabv true\n"];
        yield return ["omission-values-and-fresh-wrappers", """
            function probe(Number: any) {
                const omitted: any = new Number(), again: any = new Number();
                const undef: any = new Number(undefined), nil: any = new Number(null);
                const zero: any = new Number(-0), big: any = new Number(8n);
                console.log(omitted.valueOf(), Number.isNaN(undef.valueOf()), nil.valueOf(),
                    Object.is(zero.valueOf(), -0), big.valueOf());
                console.log(typeof omitted, omitted !== again, omitted instanceof Number,
                    Object.getPrototypeOf(omitted) === Number.prototype, omitted.constructor === Number);
                const copied: any = new Number(big, "ignored");
                console.log(copied.valueOf(), copied !== big, new Number(" 9 ", false).valueOf());
            }
            probe(Number);
            """, "0 true 0 true 8\nobject true true true true\n8 true 9\n"];
        yield return ["spread-order-and-abrupt-completion", """
            function probe(Number: any) {
                const trace = { value: "" };
                const input: any = { valueOf() { trace.value += "v"; return 17; } };
                function* args() { trace.value += "s"; yield input; trace.value += "t"; yield 99; trace.value += "e"; }
                function tail(): any { trace.value += "z"; return 0; }
                const value: any = new Number(...args(), tail());
                console.log(trace.value, typeof value, value.valueOf());
                console.log(new Number(...[]).valueOf(), Number.isNaN(new Number(...[undefined]).valueOf()),
                    new Number(...["18", 0]).valueOf());
                trace.value = "";
                function* ignored() { trace.value += "i"; yield 1; trace.value += "j"; yield 2; trace.value += "k"; }
                console.log(new Number("19", ...ignored(), tail()).valueOf(), trace.value);
                trace.value = "";
                const token: any = {};
                function* broken() { trace.value += "b"; yield input; trace.value += "t"; throw token; }
                try { new Number(...broken(), tail()); }
                catch (error) { console.log(trace.value, error === token); }
            }
            probe(Number);
            """, "stezv object 17\n0 true 18\n19 ijkz\nbt true\n"];
        yield return ["async-argument-suspension", """
            let trace = "";
            const input: any = { value: 11, valueOf() { trace += "v"; return this.value; } };
            async function construct(Number: any, pending: Promise<number>) {
                const value: any = new Number((trace += "a", input), await (trace += "b", pending),
                    (trace += "c", 0));
                console.log(trace, value.valueOf());
            }
            async function run(Number: any) {
                let release: (value: number) => void = () => {};
                const pending = new Promise<number>(resolve => { release = resolve; });
                const task = construct(Number, pending);
                console.log("pending", trace);
                input.value = 13; release(2);
                await task;
            }
            run(Number);
            """, "pending ab\nabcv 13\n"];
        yield return ["async-arrow-argument-suspension", """
            let trace = "";
            const input: any = { value: 21, valueOf() { trace += "v"; return this.value; } };
            const construct = async (Number: any, pending: Promise<number>) => {
                const value: any = new Number((trace += "a", input), await (trace += "b", pending),
                    (trace += "c", 0));
                console.log(trace, value.valueOf());
            };
            async function run(Number: any) {
                let release: (value: number) => void = () => {};
                const pending = new Promise<number>(resolve => { release = resolve; });
                const task = construct(Number, pending);
                console.log("pending", trace);
                input.value = 23; release(2);
                await task;
            }
            run(Number);
            """, "pending ab\nabcv 23\n"];
        yield return ["generator-argument-suspension", """
            let trace = "";
            const input: any = { value: 31, valueOf() { trace += "v"; return this.value; } };
            function* construct(Number: any): Generator<any, any, any> {
                return new Number((trace += "a", input), (yield "argument") as any,
                    (trace += "c", 0));
            }
            const iterator: any = construct(Number);
            console.log(iterator.next().value, trace);
            input.value = 33;
            const result: any = iterator.next(2);
            console.log(result.done, result.value.valueOf(), trace);
            """, "argument a\ntrue 33 acv\n"];
        yield return ["async-generator-argument-suspension", """
            let trace = "";
            const input: any = { value: 41, valueOf() { trace += "v"; return this.value; } };
            let release: (value: number) => void = () => {};
            let signal: (value: number) => void = () => {};
            const ready = new Promise<number>(resolve => { signal = resolve; });
            function wait(): Promise<number> {
                return new Promise<number>(resolve => { release = resolve; signal(0); });
            }
            async function* construct(Number: any): AsyncGenerator<any, any, any> {
                return new Number((trace += "a", input), (yield "argument") as any,
                    await (trace += "b", wait()), (trace += "c", 0));
            }
            async function run(Number: any) {
                const iterator: any = construct(Number);
                console.log((await iterator.next()).value, trace);
                const task = iterator.next(2);
                await ready;
                console.log("pending", trace);
                input.value = 43; release(2);
                const result: any = await task;
                console.log(result.done, result.value.valueOf(), trace);
            }
            run(Number);
            """, "argument a\npending ab\ntrue 43 abcv\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void DirectNumberConstructionEvaluatesEveryArgumentBeforeConversion(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
