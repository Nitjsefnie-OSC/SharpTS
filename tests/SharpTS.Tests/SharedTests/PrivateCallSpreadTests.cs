using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class PrivateCallSpreadTests
{
    public const string InstanceIssueSource = """
        class C {
            #m(first: any, ...rest: any[]) {
                return [Array.isArray(first), first === 3, rest.join(",")].join("|");
            }
            read() { return this.#m(...[3, 4, 5]); }
        }
        console.log(new C().read());
        """;

    public const string InstancePackingSource = """
        const marker = {};
        let effects = 0;
        class C {
            #fixed(first: any = 7, second: any = 8) { return [first, second].join("|"); }
            #rest(...values: any[]) { return values; }
            #observe(first: any, ...rest: any[]) {
                return [arguments.length, first === marker, rest[0] === marker].join("|");
            }
            read() {
                console.log(this.#fixed(...([] as [])),
                    this.#fixed(...([undefined, 4] as [any, any])),
                    this.#fixed(...([1, 2, (effects++, 9)] as any as [any, any])), effects);
                const empty = this.#rest(), one = this.#rest(marker);
                const many = this.#rest(marker, 4, 5), again = this.#rest(marker, 4, 5);
                console.log(Array.isArray(empty), empty.length, one.length, one[0] === marker,
                    many !== again, many[0] === marker, many.slice(1).join(","));
                console.log(this.#observe(marker, ...[marker, 9]));
            }
        }
        new C().read();
        """;

    public const string InstanceIteratorOrderSource = """
        const token = {};
        let order: string[] = [];
        const custom: any = {
            [Symbol.iterator]() { order.push("iterator"); let index = 0; return {
                next() { order.push("next" + index); return index < 2 ? { value: 4 + index++, done: false } : { done: true }; }
            }; }
        };
        const broken: any = { [Symbol.iterator]() { order.push("iterator"); return {
            next() { order.push("next"); throw token; }
        }; } };
        function receiver(object: any) { order.push("receiver"); return object; }
        function first() { order.push("before"); return 1; }
        function last() { order.push("after"); return 9; }
        class C {
            #m(...args: any[]) { order.push("body"); return args.join(","); }
            read(object: any, source: any) { return receiver(object).#m(first(), ...source, last()); }
        }
        const reader = new C();
        console.log(reader.read(reader, custom), order.join(","));
        order = [];
        try { reader.read({}, custom); }
        catch (error) { console.log(error instanceof TypeError, order.join(",")); }
        order = [];
        try { reader.read(reader, broken); }
        catch (error) { console.log(error === token, order.join(",")); }
        """;

    public const string InstanceSuspendedReadersSource = """
        class C {
            #m(first: any, ...rest: any[]) {
                return [this instanceof C, first, rest.join(",")].join("|");
            }
            async read() {
                return this.#m(1, ...await Promise.resolve([2, 3]), await Promise.resolve(4));
            }
            *generator() { yield 0; return this.#m(...[5, 6, 7]); }
        }
        async function run() {
            const reader = new C();
            console.log(await reader.read());
            const iterator = reader.generator();
            console.log(iterator.next().value);
            const completed = iterator.next();
            console.log(completed.value, completed.done);
        }
        run();
        """;

    public const string InstancePendingArgumentSource = """
        let order = "", receivers = 0, scheduled = 0;
        function receiver(object: any) { receivers++; order += "R"; return object; }
        function later(mark: string, value: any): Promise<any> {
            order += mark;
            scheduled++;
            return new Promise<any>(resolve => {
                setTimeout(() => { order += mark.toLowerCase(); resolve(value); }, 1);
            });
        }
        class C {
            #m(first: any, ...rest: any[]) {
                order += "B";
                return [this instanceof C, first, rest.join(","), rest.length].join("|");
            }
            async read(object: any): Promise<string> {
                return receiver(object).#m(await later("F", 1), ...await later("S", [2, 3]), await later("L", 4));
            }
            async *values(object: any): AsyncGenerator<number, string, unknown> {
                yield 0;
                return receiver(object).#m(await later("F", 5), ...await later("S", [6, 7]), await later("L", 8));
            }
        }
        async function run(): Promise<void> {
            const reader = new C();
            console.log(await reader.read(reader), order, receivers, scheduled);
            order = "";
            const iterator = reader.values(reader);
            console.log((await iterator.next()).value);
            const completed = await iterator.next();
            console.log(completed.value, completed.done, order, receivers, scheduled);
            order = "";
            try { await reader.read(null); }
            catch (error: any) { console.log(error instanceof TypeError, order, receivers, scheduled); }
            const invalid = reader.values(null);
            await invalid.next();
            order = "";
            try { await invalid.next(); }
            catch (error: any) { console.log(error instanceof TypeError, order, receivers, scheduled); }
        }
        run().catch((error: any) => console.log("unexpected", error.name, error.message));
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["instance-issue", InstanceIssueSource, "false|true|4,5\n"];
        yield return ["instance-packing", InstancePackingSource,
            "7|8 7|4 1|2 1\ntrue 0 1 true true true 4,5\n3|true|true\n"];
        yield return ["instance-iterator-order", InstanceIteratorOrderSource,
            "1,4,5,9 receiver,before,iterator,next0,next1,next2,after,body\n" +
            "true receiver\ntrue receiver,before,iterator,next\n"];
        yield return ["instance-suspended-readers", InstanceSuspendedReadersSource,
            "true|1|2,3,4\n0\ntrue|5|6,7 true\n"];
        yield return ["instance-pending-arguments", InstancePendingArgumentSource,
            "true|1|2,3,4|3 RFfSsLlB 1 3\n0\ntrue|5|6,7,8|3 true RFfSsLlB 2 6\ntrue R 3 6\ntrue R 4 6\n"];
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
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void PrivateCallsExpandGuestIterablesInArgumentOrder(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
