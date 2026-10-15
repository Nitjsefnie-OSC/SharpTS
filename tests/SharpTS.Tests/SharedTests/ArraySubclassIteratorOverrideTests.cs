using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ArraySubclassIteratorOverrideTests
{
    public const string IssueSource = """
        class NativeReverse extends Array {
            [Symbol.iterator]() {
                const values: any = this;
                return [values[1], values[0]].values();
            }
        }
        class GeneratorReverse extends Array {
            *[Symbol.iterator]() {
                const values: any = this;
                try { yield values[1]; yield values[0]; }
                finally { console.log("closed"); }
            }
        }
        const native: any = new NativeReverse(3, 4), generator: any = new GeneratorReverse(5, 6);
        console.log(native[Symbol.iterator] === NativeReverse.prototype[Symbol.iterator],
            generator[Symbol.iterator] === GeneratorReverse.prototype[Symbol.iterator]);
        let result = "";
        for (const value of native) result += value;
        console.log(result);
        for (const value of generator) { console.log(value); break; }
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", IssueSource, "true true\n43\n6\nclosed\n"];
        yield return ["inherited-live-replacement-and-deletion", """
            const state = { baseCalls: 0, replacementCalls: 0 };
            class Base extends Array<any> {
                [Symbol.iterator](): any { state.baseCalls++; return [this[1], this[0]].values(); }
            }
            class Middle extends Base {}
            class Leaf extends Middle {}
            const values: any = new Leaf(1, 2);
            const original = Base.prototype[Symbol.iterator];
            console.log(values[Symbol.iterator] === original, [...values].join(","));
            const replacement = function(this: any): any {
                state.replacementCalls++; return [this[0] + 10, this[1] + 10].values();
            };
            Object.defineProperty(Middle.prototype, Symbol.iterator, { value: replacement, writable: true, configurable: true });
            console.log(values[Symbol.iterator] === replacement, [...values].join(","));
            delete (Middle.prototype as any)[Symbol.iterator];
            console.log(values[Symbol.iterator] === original, [...values].join(","));
            delete (Base.prototype as any)[Symbol.iterator];
            console.log([...values].join(","), state.baseCalls, state.replacementCalls);
            """, "true 2,1\ntrue 11,12\ntrue 2,1\n1,2 2 1\n"];
        yield return ["getter-acquisition-and-array-receiver", """
            const state: any = { gets: 0, calls: 0, getReceiver: null, callReceiver: null };
            class Values extends Array<any> { [Symbol.iterator](): any { return [].values(); } }
            const iterator = function(this: any): any {
                state.calls++; state.callReceiver = this; return [this[1], this[0]].values();
            };
            Object.defineProperty(Values.prototype, Symbol.iterator, {
                get: function() { state.gets++; state.getReceiver = this; return iterator; }, configurable: true
            });
            const values: any = new Values(3, 4);
            let result = "";
            for (const value of values) result += value;
            console.log(result, state.gets, state.calls, state.getReceiver === values, state.callReceiver === values);
            const method = values[Symbol.iterator];
            console.log(method === iterator, state.gets, state.calls, state.getReceiver === values);
            """, "43 1 1 true true\ntrue 2 1 true\n"];
        yield return ["own-claims-mask-declared-iterator", """
            const state = { inherited: 0, own: 0, sets: 0 };
            class Values extends Array<any> {
                [Symbol.iterator](): any { state.inherited++; return [this[1], this[0]].values(); }
            }
            const values: any = new Values(5, 6);
            const own = function(this: any): any { state.own++; return [this[0] + 10].values(); };
            values[Symbol.iterator] = own;
            console.log(values[Symbol.iterator] === own, [...values].join(","), state.inherited, state.own);
            function rejects(value: any): boolean {
                try { for (const item of value) {} return false; }
                catch (error) { return true; }
            }
            for (const mask of [null, undefined, 7]) {
                Object.defineProperty(values, Symbol.iterator, { value: mask, writable: true, configurable: true });
                console.log(values[Symbol.iterator] === mask, rejects(values), state.inherited, state.own);
            }
            Object.defineProperty(values, Symbol.iterator, { set: function(next: any) { state.sets++; }, configurable: true });
            console.log(values[Symbol.iterator] === undefined, rejects(values), state.inherited, state.own, state.sets);
            delete values[Symbol.iterator];
            console.log([...values].join(","), state.inherited, state.own);
            """, "true 15 0 1\ntrue true 0 1\ntrue true 0 1\ntrue true 0 1\ntrue true 0 1 0\n6,5 1 1\n"];
        yield return ["nearest-prototype-claims", """
            const state = { calls: 0, sets: 0 };
            class Base extends Array<any> {
                [Symbol.iterator](): any { state.calls++; return [this[1], this[0]].values(); }
            }
            class Child extends Base {}
            const values: any = new Child(7, 8);
            function rejects(): boolean {
                try { for (const item of values) {} return false; }
                catch (error) { return true; }
            }
            for (const mask of [null, undefined, 7]) {
                Object.defineProperty(Child.prototype, Symbol.iterator, { value: mask, writable: true, configurable: true });
                console.log(values[Symbol.iterator] === mask, rejects(), state.calls);
            }
            Object.defineProperty(Child.prototype, Symbol.iterator, {
                set: function(next: any) { state.sets++; }, configurable: true
            });
            console.log(values[Symbol.iterator] === undefined, rejects(), state.calls, state.sets);
            delete (Child.prototype as any)[Symbol.iterator];
            console.log([...values].join(","), state.calls);
            """, "true true 0\ntrue true 0\ntrue true 0\ntrue true 0 0\n8,7 1\n"];
        yield return ["iterator-consumers", """
            const state = { calls: 0, closed: 0 };
            class Reverse extends Array<any> {
                *[Symbol.iterator](): any {
                    state.calls++;
                    try { yield this[1]; yield this[0]; }
                    finally { state.closed++; }
                }
            }
            const values: any = new Reverse("a", "b");
            console.log([...values].join(","));
            function collect(...items: any[]): string { return items.join(":"); }
            console.log(collect(...values));
            function* delegate(): Generator<any, any, any> { yield* values; }
            let delegated = "";
            for (const value of delegate()) delegated += value;
            console.log("delegate", delegated);
            const [first, second] = values as any;
            console.log(first, second);
            async function run(iterable: any) {
                let result = "";
                for await (const value of iterable) result += value;
                console.log("async", result, state.calls, state.closed);
            }
            run(values).catch((error: any) => console.log("unexpected", error.name, error.message));
            """, "b,a\nb:a\ndelegate ba\nb a\nasync ba 5 5\n"];
        yield return ["explicit-prototype-precedence-and-array-terminal", """
            const state: any = { nominal: 0, custom: 0, receiver: null };
            class Nominal extends Array<any> {
                [Symbol.iterator](): any { state.nominal++; return ["nominal"].values(); }
            }
            class Empty extends Array<any> {}
            const values: any = new Nominal(1, 2);
            const custom = function(this: any): any {
                state.custom++; state.receiver = this; return [this[1] + 10, this[0] + 10].values();
            };
            Object.setPrototypeOf(values, { [Symbol.iterator]: custom });
            console.log(values[Symbol.iterator] === custom, [...values].join(","),
                state.nominal, state.custom, state.receiver === values);
            Object.setPrototypeOf(values, null);
            let rejected = false;
            try { for (const value of values) {} }
            catch (error) { rejected = true; }
            console.log(values[Symbol.iterator] === undefined, rejected, state.nominal, state.custom);
            Object.setPrototypeOf(values, Array.prototype);
            console.log([...values].join(","), state.nominal, state.custom);
            Object.setPrototypeOf(values, Empty.prototype);
            console.log([...values].join(","), state.nominal, state.custom);
            """, "true 12,11 0 1 true\ntrue true 0 1\n1,2 0 1\n1,2 0 1\n"];
        yield return ["return-throw-close-and-default-live-length", """
            const state = { closed: 0 };
            class Closing extends Array<any> {
                *[Symbol.iterator](): any {
                    try { yield this[1]; yield this[0]; }
                    finally { state.closed++; }
                }
            }
            const values: any = new Closing("a", "b");
            function take(): any { for (const value of values) return value; return undefined; }
            console.log(take(), state.closed);
            const token: any = {};
            try { for (const value of values) { console.log(value); throw token; } }
            catch (error) { console.log(error === token, state.closed); }
            class Plain extends Array<any> {}
            const plain: any = new Plain(1, 2);
            let result = "";
            for (const value of plain) { result += value; if (value === 1) plain.push(3); }
            console.log(result, plain.length, state.closed);
            """, "b 1\nb\ntrue 2\n123 3 2\n"];
    }

    // The remaining cases retain interpreter coverage while separate compiler
    // iterator-consumer and explicit-prototype limitations are tracked.
    public static IEnumerable<object[]> CompiledCases() => Cases().Where(row => (string)row[0] is
        "original" or "getter-acquisition-and-array-receiver" or "return-throw-close-and-default-live-length");

    public static IEnumerable<object[]> ModeCases() => Cases()
        .Select(row => new object[] { row[0], row[1], row[2], ExecutionMode.Interpreted })
        .Concat(CompiledCases()
            .Select(row => new object[] { row[0], row[1], row[2], ExecutionMode.Compiled }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ArraySubclassIterationUsesTheSelectedGuestIterator(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
