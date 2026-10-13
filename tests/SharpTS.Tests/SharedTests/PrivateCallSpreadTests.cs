using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class PrivateCallSpreadTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["fields-methods-reference", """
            class C {
                static #staticMethod(first: any, ...rest: any[]) { return [Array.isArray(first), first === 3, rest.join(","), arguments.length].join("|"); }
                static #staticField: Function = function(first: any, ...rest: any[]) { return [Array.isArray(first), first === 3, rest.join(","), arguments.length].join("|"); };
                #field: Function = function(first: any, ...rest: any[]) { return [Array.isArray(first), first === 3, rest.join(","), arguments.length].join("|"); };
                static read() { return [this.#staticMethod(...[3, 4, 5]), this.#staticField(...[3, 4, 5])]; }
                read() { return [this.#field(...[3, 4, 5])]; }
            }
            for (const result of [...C.read(), ...new C().read()]) console.log(result);
            """, "false|true|4,5|3\nfalse|true|4,5|3\nfalse|true|4,5|3\n"];
        yield return ["iterable-order-reference", """
            let order: string[] = [];
            const custom: any = {
                [Symbol.iterator]() { order.push("iterator"); let index = 0; return {
                    next() { order.push("next" + index); return index < 2 ? { value: 4 + index++, done: false } : { done: true }; }
                }; }
            };
            function first() { order.push("before"); return 1; }
            function last() { order.push("after"); return 9; }
            class C {
                static #m(...args: any[]) { order.push("body"); return args.join(","); }
                static read() { return this.#m(first(), ...[2, 3], ...custom, ..."ab", last()); }
            }
            console.log(C.read(), order.join(","));
            """, "1,2,3,4,5,a,b,9 before,iterator,next0,next1,next2,after,body\n"];
        yield return ["abrupt-reference", """
            const token: any = { failure: true };
            let order: string[] = [];
            const broken: any = { [Symbol.iterator]() { order.push("iterator"); return {
                next() { order.push("next"); throw token; }
            }; } };
            class C {
                static #method(...args: any[]) { order.push("body"); }
                static #field: Function = (...args: any[]) => { order.push("body"); };
                static method() { return this.#method((order.push("before"), 1), ...broken, (order.push("after"), 2)); }
                static field() { return this.#field((order.push("before"), 1), ...broken, (order.push("after"), 2)); }
            }
            for (const read of [C.method, C.field]) {
                order = [];
                try { read.call(C); console.log(false); }
                catch (error) { console.log(error === token, order.join(",")); }
            }
            """, "true before,iterator,next\ntrue before,iterator,next\n"];
        yield return ["awaited-reference", """
            class C {
                static #m(...args: any[]) { return args.join(","); }
                #field: Function = function(this: any, ...args: any[]) { return [this instanceof C, args.join(",")].join("|"); };
                static async read() { return this.#m(1, ...await Promise.resolve([2, 3]), await Promise.resolve(4), ..."ab"); }
                async read() { return this.#field(...await Promise.resolve([1, 2]), ..."ab"); }
            }
            async function run() { console.log(await C.read()); console.log(await new C().read()); }
            run();
            """, "1,2,3,4,a,b\ntrue|1,2,a,b\n"];
        yield return ["generator-reference", """
            let order = "";
            function* values() {
                order += "G";
                try { yield 2; yield 3; }
                finally { order += "F"; }
            }
            function last() { order += "L"; return 4; }
            class C {
                static #m(...args: any[]) { order += "B"; return args.join(",") + ":" + arguments.length; }
                static read() { return this.#m(1, ...values(), last()); }
            }
            console.log(C.read(), order);
            """, "1,2,3,4:4 GFLB\n"];
        yield return ["instance-method-reference", """
            class C {
                #method(first: any, ...rest: any[]) {
                    return [Array.isArray(first), first === 3, rest.join(","), arguments.length].join("|");
                }
                read() { return this.#method(...[3, 4, 5]); }
            }
            console.log(new C().read());
            """, "false|true|4,5|3\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        // Direct instance private rest/spread calls have a compiler follow-up (#2153).
        ((string)row[0] == "instance-method-reference"
            ? new[] { ExecutionMode.Interpreted }
            : new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled })
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void PrivateCallsExpandGuestIterablesInArgumentOrder(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
