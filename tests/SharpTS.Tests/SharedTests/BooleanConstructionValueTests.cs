using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class BooleanConstructionValueTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", """
            const Alias: any = Boolean;
            const aliased: any = new Alias("");
            console.log(typeof aliased, aliased.valueOf());
            const bind: any = (() => 0).bind;
            const Bound: any = bind.call(Boolean, {}, "");
            const bound: any = new Bound();
            console.log(typeof bound, bound.valueOf());
            """, "object false\nobject false\n"];
        yield return ["truthiness-and-identity", """
            const C: any = Boolean;
            const falsy: any[] = [undefined, null, false, 0, -0, NaN, "", 0n];
            const truthy: any[] = [true, 1, -1, "false", "0", {}, [], Symbol("value"), 1n, new Boolean(false)];
            console.log(new C().valueOf(), falsy.map(value => new C(value).valueOf()).join(","));
            console.log(truthy.map(value => new C(value).valueOf()).join(","));
            let hooks = 0;
            const input: any = {
                valueOf() { hooks++; throw "valueOf"; },
                toString() { hooks++; throw "toString"; },
                [Symbol.toPrimitive]() { hooks++; throw "toPrimitive"; }
            };
            console.log(new C(input).valueOf(), hooks);
            const first: any = new C(false), second: any = new C(false);
            console.log(typeof first, first !== second, first instanceof Boolean, first instanceof Object,
                Object.getPrototypeOf(first) === Boolean.prototype, first.constructor === Boolean);
            console.log(first.valueOf(), second.valueOf(), new C(first).valueOf());
            """, "false false,false,false,false,false,false,false,false\ntrue,true,true,true,true,true,true,true,true,true\ntrue 0\nobject true true true true true\nfalse false true\n"];
        yield return ["runtime-and-argument-order", """
            let order = "";
            function getCtor(): any { order += "c"; return Boolean; }
            function first(): any { order += "a"; return ""; }
            function second(): any { order += "b"; return 99; }
            const value: any = new (getCtor())(first(), second());
            console.log(order, typeof value, value.valueOf());
            const holder: any = { C: Boolean }, constructors: any = [Boolean];
            let Alias: any = String;
            Alias = Boolean;
            console.log(new holder.C(null).valueOf(), new constructors[0]("0").valueOf(),
                new Alias(0).valueOf());
            const token: any = {};
            function fail(): any { order += "x"; throw token; }
            order = "";
            try { new (getCtor())(first(), fail()); }
            catch (error) { console.log(order, error === token); }
            """, "cab object false\nfalse true false\ncax true\n"];
        yield return ["bound-and-reflect", """
            const bind: any = (() => 0).bind;
            const False: any = bind.call(Boolean, {}, "");
            const Empty: any = bind.call(Boolean, {});
            const Undef: any = bind.call(Boolean, {}, undefined);
            const True: any = bind.call(Boolean, {}, true);
            const Rebound: any = False.bind({}, 1), ReboundEmpty: any = Empty.bind({}, 0);
            console.log(new False(1).valueOf(), new Empty(1).valueOf(), new Undef(1).valueOf(),
                new True("").valueOf(), new Rebound(false).valueOf(), new ReboundEmpty(1).valueOf());
            console.log(Reflect.construct(Boolean, []).valueOf(),
                Reflect.construct(Boolean, ["false"]).valueOf(),
                Reflect.construct(Boolean, [0], Boolean).valueOf());
            console.log((Reflect.construct(False, [1]) as any).valueOf(), (Reflect.construct(Empty, [1]) as any).valueOf(),
                (Reflect.construct(Rebound, [1]) as any).valueOf());
            """, "false true false true false false\nfalse true false\nfalse true false\n"];
        yield return ["spread", """
            const C: any = Boolean;
            let trace = "";
            function* args() { trace += "s"; yield ""; trace += "t"; yield 99; trace += "e"; }
            const value: any = new C(...args());
            console.log(trace, typeof value, value.valueOf());
            console.log(new C(...[]).valueOf(), new C(...[undefined]).valueOf(),
                new C(...["false", 0]).valueOf());
            const bind: any = (() => 0).bind;
            const Empty: any = bind.call(Boolean, {}), Bound: any = bind.call(Boolean, {}, "false");
            console.log(new Empty(...[]).valueOf(), new Empty(...[1]).valueOf(), new Bound(...[0]).valueOf());
            trace = "";
            const token: any = {};
            function* broken() { trace += "b"; yield ""; trace += "t"; throw token; }
            try { new C(...broken()); }
            catch (error) { console.log(trace, error === token); }
            """, "ste object false\nfalse false true\nfalse true true\nbt true\n"];
        yield return ["suspended", """
            function* make(C: any) {
                yield 0;
                return new C("");
            }
            const iterator: any = make(Boolean);
            iterator.next();
            const generated: any = iterator.next().value;
            console.log("generator", typeof generated, generated.valueOf());
            function* fromYield(C: any): Generator<string, any, any> {
                // #2251 tracks retaining a constructor across a yielded argument.
                const argument: any = yield "argument";
                return new C(argument);
            }
            const paused: any = fromYield(Boolean);
            console.log(paused.next().value, paused.next("false").value.valueOf());
            async function* values(C: any) {
                await Promise.resolve(0);
                yield new C(0);
                yield new C({});
            }
            async function run(C: any) {
                await Promise.resolve(0);
                const value: any = new C(await Promise.resolve("false"));
                const omitted: any = new C(), undef: any = new C(undefined);
                console.log("async", typeof value, value.valueOf(), omitted.valueOf(), undef.valueOf());
                const iterator: any = values(C);
                console.log("async-generator", (await iterator.next()).value.valueOf(),
                    (await iterator.next()).value.valueOf());
            }
            run(Boolean);
            """, "generator object false\nargument true\nasync object true false false\nasync-generator false true\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void ConstructorValuesCreateFreshBooleanWrappers(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
