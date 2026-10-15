using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ApplyArrayLikeArgumentListTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            function read(value: any) { return value; }
            const f: any = read, args: any = { 0: 42, length: 1 };
            try { console.log(f.apply(null, args)); }
            catch (error: any) { console.log(error.name, error.message); }
            try { console.log(Reflect.apply(Function.prototype.apply, f, [null, args])); }
            catch (error: any) { console.log(error.name, error.message); }
            """, "42\n42\n"];
        yield return ["array-like-representations", """
            const receiver: any = { tag: "R" };
            function format(this: any, ...values: any[]): string {
                return this.tag + ":" + values.map(v => v === undefined ? "U" : String(v)).join("|")
                    + ":" + values.length;
            }
            const f: any = format;
            const inherited: any = Object.create({ 1: "inherited" });
            inherited[0] = "own"; inherited.length = 4;
            Object.defineProperty(inherited, "2", { value: "hidden", enumerable: false });
            function getArguments(...values: any[]): any { return arguments; }
            class Source {
                get length(): number { return 2; }
                get 0(): string { return "source"; }
                get 1(): string { return "getter"; }
            }
            for (const args of [{ 0: "first", 2: "last", length: 3 }, inherited,
                new String("xy"), getArguments("a", "b"), new Source()]) {
                console.log(f.apply(receiver, args),
                    Reflect.apply(Function.prototype.apply, f, [receiver, args]));
            }
            """, "R:first|U|last:3 R:first|U|last:3\nR:own|inherited|hidden|U:4 R:own|inherited|hidden|U:4\nR:x|y:2 R:x|y:2\nR:a|b:2 R:a|b:2\nR:source|getter:2 R:source|getter:2\n"];
        yield return ["length-index-order-and-snapshot", """
            const state: any = { trace: "", second: "old", indexReceiver: null };
            function makeArguments(): any {
                const prototype: any = {};
                Object.defineProperty(prototype, "1", { get: function() {
                    state.trace += "b"; state.indexReceiver = this; return state.second;
                } });
                Object.defineProperty(prototype, "2", { get: function() {
                    state.trace += "c"; return undefined;
                } });
                const args: any = Object.create(prototype);
                Object.defineProperty(args, "length", { configurable: true, get: function() {
                    state.trace += "l";
                    return { valueOf() { state.trace += "v"; return 3; } };
                } });
                Object.defineProperty(args, "0", { get: function() {
                    state.trace += "a"; state.second = "changed";
                    Object.defineProperty(args, "length", { value: 1 }); return "first";
                } });
                Object.defineProperty(args, Symbol.iterator, { get: function() {
                    state.trace += "i"; throw "iterator must not be read";
                } });
                return args;
            }
            const target: any = function(a: any, b: any, c: any) {
                state.trace += "t"; return [a, b, c === undefined].join("|");
            };
            for (const borrowed of [false, true]) {
                state.trace = ""; state.second = "old"; state.indexReceiver = null;
                const args: any = makeArguments();
                const result = borrowed ? Reflect.apply(Function.prototype.apply, target, [null, args])
                    : target.apply(null, args);
                console.log(result, state.trace, state.indexReceiver === args);
            }
            """, "first|changed|true lvabct true\nfirst|changed|true lvabct true\n"];
        yield return ["length-normalization-and-rejected-coercions", """
            const state = { calls: 0 };
            const target: any = function(...values: any[]) { state.calls++; return values.length; };
            for (const length of [undefined, NaN, -3, 1.9, "2"]) {
                const args: any = { 0: "a", 1: "b", length };
                console.log(target.apply(null, args),
                    Reflect.apply(Function.prototype.apply, target, [null, args]));
            }
            function rejects(run: () => any): boolean {
                try { run(); return false; } catch (error) { return error instanceof TypeError; }
            }
            state.calls = 0;
            for (const length of [1n, Symbol("length")]) {
                const args: any = { length };
                console.log(rejects(() => target.apply(null, args)),
                    rejects(() => Reflect.apply(Function.prototype.apply, target, [null, args])), state.calls);
            }
            """, "0 0\n0 0\n0 0\n1 1\n2 2\ntrue true 0\ntrue true 0\n"];
        yield return ["callable-validation-and-primitive-lists", """
            const state = { lengthReads: 0, calls: 0 };
            const token = {};
            const args: any = { get length() { state.lengthReads++; throw token; } };
            const apply: any = Function.prototype.apply;
            const invalid: any = { apply };
            try { invalid.apply(null, args); }
            catch (error) { console.log(error instanceof TypeError, state.lengthReads); }
            try { Reflect.apply(apply, {}, [null, args]); }
            catch (error) { console.log(error instanceof TypeError, state.lengthReads); }
            const target: any = function() { state.calls++; };
            function rejects(run: () => any): boolean {
                try { run(); return false; } catch (error) { return error instanceof TypeError; }
            }
            for (const list of [7, true, "bad", Symbol("args"), 1n]) {
                console.log(rejects(() => target.apply(null, list)),
                    rejects(() => Reflect.apply(apply, target, [null, list])), state.calls);
            }
            """, "true 0\ntrue 0\ntrue true 0\ntrue true 0\ntrue true 0\ntrue true 0\ntrue true 0\n"];
        yield return ["abrupt-length-index-and-target", """
            const state = { trace: "", calls: 0 };
            const token = {};
            const target: any = function(...values: any[]) {
                state.trace += "t"; state.calls++; throw token;
            };
            function makeArguments(stage: string): any {
                return {
                    get length() {
                        state.trace += "l"; if (stage === "length") throw token;
                        return { valueOf() {
                            state.trace += "v"; if (stage === "coercion") throw token; return 2;
                        } };
                    },
                    get 0() { state.trace += "a"; if (stage === "first") throw token; return 1; },
                    get 1() { state.trace += "b"; if (stage === "later") throw token; return 2; }
                };
            }
            for (const borrowed of [false, true]) {
                for (const stage of ["length", "coercion", "first", "later", "target"]) {
                    state.trace = ""; state.calls = 0;
                    const args: any = makeArguments(stage);
                    try {
                        if (borrowed) Reflect.apply(Function.prototype.apply, target, [null, args]);
                        else target.apply(null, args);
                    } catch (error) { console.log(error === token, state.trace, state.calls); }
                }
            }
            """, "true l 0\ntrue lv 0\ntrue lva 0\ntrue lvab 0\ntrue lvabt 1\ntrue l 0\ntrue lv 0\ntrue lva 0\ntrue lvab 0\ntrue lvabt 1\n"];
        yield return ["nullish-and-array-get-controls", """
            const receiver = {};
            const target: any = function(this: any, ...values: any[]) {
                return (this === receiver) + ":" + values.length;
            };
            const apply: any = Function.prototype.apply;
            console.log(target.apply(receiver), target.apply(receiver, undefined),
                target.apply(receiver, null), target.apply(receiver, []));
            console.log(Reflect.apply(apply, target, [receiver]),
                Reflect.apply(apply, target, [receiver, undefined]),
                Reflect.apply(apply, target, [receiver, null]), Reflect.apply(apply, target, [receiver, []]));
            const format: any = function(...values: any[]) {
                return values.map(v => v === undefined ? "U" : String(v)).join(",");
            };
            const array: any = [1, , 3];
            console.log(format.apply(null, array), Reflect.apply(apply, format, [null, array]));
            const state = { gets: 0 };
            Object.defineProperty(array, "1", { get: function() { state.gets++; return 2; } });
            console.log(format.apply(null, array), state.gets);
            console.log(Reflect.apply(apply, format, [null, array]), state.gets);
            """, "true:0 true:0 true:0 true:0\ntrue:0 true:0 true:0 true:0\n1,U,3 1,U,3\n1,2,3 1\n1,2,3 2\n"];
        yield return ["receiver-default-rest-arrow-and-bound", """
            function inspect(this: any, first: any = "default", ...rest: any[]): string {
                return [this.tag, first, rest.join(","), arguments.length].join("|");
            }
            const target: any = inspect, receiver: any = { tag: "actual" };
            const args: any = { 0: undefined, 1: "tail", length: 2 };
            console.log(target.apply(receiver, args));
            console.log(Reflect.apply(Function.prototype.apply, target, [receiver, args]));
            const bound: any = target.bind({ tag: "bound" }, "bound");
            const boundArgs: any = { 0: "x", 1: "y", length: 2 };
            console.log(bound.apply(receiver, boundArgs));
            console.log(Reflect.apply(Function.prototype.apply, bound, [receiver, boundArgs]));
            function makeArrow(this: any): any {
                return (...values: any[]) => this.tag + ":" + values.join(",");
            }
            const arrow: any = makeArrow.call({ tag: "lexical" });
            console.log(arrow.apply(receiver, { 0: "x", length: 1 }));
            console.log(Reflect.apply(Function.prototype.apply, arrow, [receiver, { 0: "y", length: 1 }]));
            const array = new Uint8Array([1, 2]);
            const fill: any = array.fill;
            fill.apply(array, [7]);
            console.log(array[0], array[1]);
            """, "actual|default|tail|2\nactual|default|tail|2\nbound|bound|x,y|3\nbound|bound|x,y|3\nlexical:x\nlexical:y\n7 7\n"];
    }

    public static IEnumerable<object[]> ModeCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ApplyCollectsArrayLikeArgumentsBeforeInvokingTheTarget(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
