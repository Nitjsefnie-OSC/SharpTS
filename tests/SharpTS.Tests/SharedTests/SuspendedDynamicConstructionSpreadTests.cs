using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class SuspendedDynamicConstructionSpreadTests
{
    public const string IssueSource = """
        function* make(C: any) {
            yield 0;
            return new C(...['x', 'g']);
        }
        const iterator: any = make(RegExp);
        iterator.next();
        const generated: any = iterator.next().value;
        console.log('generator', generated.source, JSON.stringify(generated.flags));
        async function run(C: any) {
            await Promise.resolve(0);
            const value: any = new C(...['x', 'g']);
            console.log('async', value.source, JSON.stringify(value.flags));
        }
        run(RegExp);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", IssueSource, "generator x \"g\"\nasync x \"g\"\n"];
        yield return ["arrow-and-async-generator", """
            const arrow = async (C: any) => {
                await Promise.resolve(0);
                const value: any = new C(...["arrow", "i"]);
                console.log("arrow", value.source, JSON.stringify(value.flags));
            };
            async function* make(C: any): AsyncGenerator<any, any, any> {
                yield 0;
                await Promise.resolve(0);
                return new C(...["generated", "m"]);
            }
            async function run(C: any) {
                await arrow(C);
                const iterator: any = make(C);
                await iterator.next();
                const value: any = (await iterator.next()).value;
                console.log("async-generator", value.source, JSON.stringify(value.flags));
            }
            run(RegExp).catch((error: any) => console.log("unexpected", error.name, error.message));
            """, "arrow arrow \"i\"\nasync-generator generated \"m\"\n"];
        yield return ["expanded-number-arity", """
            function* generate(C: any): Generator<any, any, any> {
                yield 0;
                return [new C(...[]).valueOf(), new C(...[undefined]).valueOf(),
                    new C(...["15", 99]).valueOf()] as any;
            }
            async function asynchronous(C: any): Promise<any> {
                await Promise.resolve(0);
                return [new C(...[]).valueOf(), new C(...[undefined]).valueOf(),
                    new C(...["15", 99]).valueOf()];
            }
            const arrow = async (C: any): Promise<any> => {
                await Promise.resolve(0);
                return [new C(...[]).valueOf(), new C(...[undefined]).valueOf(),
                    new C(...["15", 99]).valueOf()];
            };
            async function* asynchronousGenerator(C: any): AsyncGenerator<any, any, any> {
                yield 0;
                return [new C(...[]).valueOf(), new C(...[undefined]).valueOf(),
                    new C(...["15", 99]).valueOf()] as any;
            }
            function show(label: string, values: any) {
                console.log(label, values[0], values[1] !== values[1], values[2]);
            }
            async function run(C: any) {
                const iterator: any = generate(C);
                iterator.next(); show("generator", iterator.next().value);
                show("async", await asynchronous(C));
                show("arrow", await arrow(C));
                const asyncIterator: any = asynchronousGenerator(C);
                await asyncIterator.next();
                show("async-generator", (await asyncIterator.next()).value);
            }
            run(Number).catch((error: any) => console.log("unexpected", error.name, error.message));
            """, "generator 0 true 15\nasync 0 true 15\narrow 0 true 15\nasync-generator 0 true 15\n"];
        yield return ["callee-argument-order-and-abrupt-spread", """
            const state = { trace: "" };
            const token: any = {};
            function Constructor(this: any, ...values: any[]) {
                state.trace += "k";
                this.values = values; this.count = arguments.length; this.target = new.target;
            }
            function pick(): any { state.trace += "c"; return Constructor; }
            function first(): any { state.trace += "a"; return 1; }
            function last(): any { state.trace += "b"; return 4; }
            function* parts() { state.trace += "s"; yield 2; state.trace += "t"; yield 3; state.trace += "e"; }
            function* broken() { state.trace += "s"; yield 2; state.trace += "x"; throw token; }
            function* make(): Generator<any, any, any> {
                yield 0;
                return new (pick())(first(), ...parts(), last());
            }
            const iterator: any = make(); iterator.next();
            const value: any = iterator.next().value;
            console.log(state.trace, value.values.join(","), value.count, value.target === Constructor);
            function* fail(values: any): Generator<any, any, any> {
                yield 0;
                return new (pick())(first(), ...values, last());
            }
            state.trace = "";
            const failing: any = fail(broken()); failing.next();
            try { failing.next(); }
            catch (error) { console.log(state.trace, error === token); }
            state.trace = "";
            const invalid: any = fail(123); invalid.next();
            try { invalid.next(); }
            catch (error) { console.log(state.trace); }
            state.trace = "";
            const wrong: any = { [Symbol.iterator]() { state.trace += "i"; throw token; } };
            const abrupt: any = fail(wrong); abrupt.next();
            try { abrupt.next(); }
            catch (error) { console.log(state.trace, error === token); }
            """, "castebk 1,2,3,4 4 true\ncasx true\nca\ncai true\n"];
        yield return ["generator-storage-and-constructor-snapshot", """
            const state: any = { trace: "", selected: { value: 1 } };
            const saved = state.selected;
            function Original(this: any, ...values: any[]) {
                state.trace += "k"; this.kind = "original"; this.values = values; this.target = new.target;
            }
            function Replacement(this: any, ...values: any[]) { state.trace += "x"; this.kind = "replacement"; this.values = values; }
            function first(): any { state.trace += "a"; return state.selected; }
            function* prefix() { state.trace += "p"; yield 2; state.trace += "q"; yield 3; state.trace += "r"; }
            function* suffix() { state.trace += "s"; yield 8; state.trace += "t"; }
            function last(): any { state.trace += "b"; return 9; }
            function* make(C: any, R: any): Generator<any, any, any> {
                return new C(first(), ...prefix(), (state.trace += "m", C = R, (yield "middle")) as any,
                    ...suffix(), last());
            }
            const iterator: any = make(Original, Replacement);
            console.log(iterator.next().value, state.trace);
            state.selected = { value: 99 };
            const result: any = iterator.next(7).value;
            console.log(result.kind, result.values[0] === saved, result.values.slice(1).join(","),
                result.target === Original, state.trace);
            """, "middle apqrm\noriginal true 2,3,7,8,9 true apqrmstbk\n"];
        yield return ["yielded-spread-operand", """
            function* make(C: any): Generator<any, any, any> {
                return new C(...((yield "arguments") as any));
            }
            const iterator: any = make(RegExp);
            console.log(iterator.next().value);
            const value: any = iterator.next(["yielded", "g"]).value;
            console.log(value.source, JSON.stringify(value.flags));
            """, "arguments\nyielded \"g\"\n"];
        yield return ["callee-preservation-without-spread", """
            function* make(C: any): Generator<string, any, any> {
                return new C(yield "argument");
            }
            const iterator: any = make(Number);
            console.log(iterator.next().value);
            console.log(iterator.next("15").value.valueOf());
            """, "argument\n15\n"];
        yield return ["pending-async-storage-and-snapshot", """
            const state = { trace: "", first: "one" };
            function Original(this: any, ...values: any[]) { state.trace += "k"; this.kind = "original"; this.values = values; }
            function Replacement(this: any, ...values: any[]) { state.trace += "x"; this.kind = "replacement"; this.values = values; }
            function first(): any { state.trace += "a"; return state.first; }
            function* prefix() { state.trace += "p"; yield 2; state.trace += "q"; }
            function* suffix() { state.trace += "s"; yield 5; state.trace += "t"; }
            async function construct(C: any, R: any, pending: Promise<number>): Promise<any> {
                return new C(first(), ...prefix(), await (state.trace += "w", C = R, pending), ...suffix());
            }
            async function run(C: any, R: any) {
                let release: (value: number) => void = () => {};
                const pending = new Promise<number>(resolve => { release = resolve; });
                const task = construct(C, R, pending);
                console.log("pending", state.trace);
                state.first = "changed"; release(4);
                const result: any = await task;
                console.log(result.kind, result.values.join(","), state.trace);
            }
            run(Original, Replacement).catch((error: any) => console.log("unexpected", error.name, error.message));
            """, "pending apqw\noriginal one,2,4,5 apqwstk\n"];
        yield return ["pending-arrow-spread-and-snapshot", """
            const state = { trace: "" };
            function Original(this: any, ...values: any[]) { state.trace += "k"; this.kind = "original"; this.values = values; }
            function Replacement(this: any, ...values: any[]) { state.trace += "x"; this.kind = "replacement"; this.values = values; }
            const construct = async (C: any, R: any, pending: Promise<any>): Promise<any> =>
                new C((state.trace += "a", 1), ...(await pending), (state.trace += "b", C = R, 4));
            async function run(C: any, R: any) {
                let release: (value: any) => void = () => {};
                const pending = new Promise<any>(resolve => { release = resolve; });
                const task = construct(C, R, pending);
                console.log("pending", state.trace);
                release([2, 3]);
                const result: any = await task;
                console.log(result.kind, result.values.join(","), state.trace);
            }
            run(Original, Replacement).catch((error: any) => console.log("unexpected", error.name, error.message));
            """, "pending a\noriginal 1,2,3,4 abk\n"];
        yield return ["pending-async-generator-storage-and-snapshot", """
            const state = { trace: "" };
            function Original(this: any, ...values: any[]) { state.trace += "k"; this.kind = "original"; this.values = values; }
            function Replacement(this: any, ...values: any[]) { state.trace += "x"; this.kind = "replacement"; this.values = values; }
            let release: (value: number) => void = () => {};
            let signal: (value: number) => void = () => {};
            const ready = new Promise<number>(resolve => { signal = resolve; });
            function wait(): Promise<number> {
                return new Promise<number>(resolve => { release = resolve; signal(0); });
            }
            async function* construct(C: any, R: any): AsyncGenerator<any, any, any> {
                return new C((state.trace += "a", 1), ...((yield "arguments") as any),
                    await (state.trace += "w", wait()), (state.trace += "b", C = R, 5));
            }
            async function run(C: any, R: any) {
                const iterator: any = construct(C, R);
                console.log((await iterator.next()).value, state.trace);
                const task = iterator.next([2, 3]);
                await ready;
                console.log("pending", state.trace);
                release(4);
                const result: any = (await task).value;
                console.log(result.kind, result.values.join(","), state.trace);
            }
            run(Original, Replacement).catch((error: any) => console.log("unexpected", error.name, error.message));
            """, "arguments a\npending aw\noriginal 1,2,3,4,5 awbk\n"];
        yield return ["class-expression-argument-suspension", """
            const C = class {
                values: any[];
                target: any;
                constructor(first: any, second?: any, third?: any, fourth?: any) {
                    this.values = [first, second, third, fourth]; this.target = new.target;
                }
            };
            function* make(): Generator<any, any, any> {
                return new C("first", ...(((yield "arguments") as any) as [any, any]), 4) as any;
            }
            const iterator: any = make();
            console.log(iterator.next().value);
            const value: any = iterator.next([2, 3]).value;
            console.log(value.values.join(","), value.target === C);
            """, "arguments\nfirst,2,3,4 true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void SuspendedDynamicConstructionExpandsArgumentsAndPreservesTheConstructor(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
