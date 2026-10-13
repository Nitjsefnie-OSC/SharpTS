using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class NumberConstructionValueTests
{
    public const string IssueSource = """
        const C: any = Number;
        const value: any = new C(7);
        console.log(typeof value, value.valueOf());
        """;

    public const string CoercionSource = """
        const C: any = Number;
        const omitted: any = new C(), undef: any = new C(undefined), nil: any = new C(null);
        const text: any = new C(" 7 "), big: any = new C(8n), truth: any = new C(true);
        const zero: any = new C(-0), original: any = new Number(9), copied: any = new C(original);
        console.log(typeof omitted, omitted.valueOf(), Number.isNaN(undef.valueOf()), nil.valueOf());
        console.log(text.valueOf(), big.valueOf(), truth.valueOf(), Object.is(zero.valueOf(), -0));
        console.log(copied.valueOf(), copied !== original, new C(1) !== new C(1), copied instanceof Number);
        try { new C(Symbol("value")); }
        catch (error) { console.log(error instanceof TypeError); }
        """;

    public const string RuntimeSource = """
        let order = "";
        function getCtor(): any { order += "c"; return Number; }
        const input: any = { valueOf() { order += "v"; return 13; } };
        function first(): any { order += "a"; return input; }
        function second(): any { order += "b"; return 99; }
        const value: any = new (getCtor())(first(), second());
        console.log(order, typeof value, value.valueOf());
        const holder: any = { C: Number }, constructors: any = [Number];
        console.log(new holder.C(null).valueOf(), new constructors[0](true).valueOf());
        const C: any = Number;
        function fail(): any { throw "extra"; }
        try { new C(1, fail()); }
        catch (error) { console.log(error === "extra"); }
        """;

    public const string BoundSource = """
        const bind: any = (() => 0).bind;
        const C: any = bind.call(Number, {}, 7);
        const value: any = new C();
        console.log(typeof value, value.valueOf());
        const B: any = bind.call(Number, {}, "11"), E: any = bind.call(Number, {});
        const U: any = bind.call(Number, {}, undefined), rebound: any = B.bind({}, "ignored");
        console.log(new B("ignored").valueOf(), new E().valueOf(),
            Number.isNaN(new U("9").valueOf()), new rebound().valueOf());
        """;

    public const string SpreadSource = """
        const C: any = Number;
        let trace = "";
        const input: any = { valueOf() { trace += "v"; return 17; } };
        function* args() { trace += "s"; yield input; trace += "t"; yield 99; trace += "e"; }
        const value: any = new C(...args());
        console.log(trace, typeof value, value.valueOf());
        console.log(new C(...[]).valueOf(), Number.isNaN(new C(...[undefined]).valueOf()));
        const bind: any = (() => 0).bind;
        const E: any = bind.call(Number, {}), B: any = bind.call(Number, {}, "19");
        console.log(new E(...["18"]).valueOf(), new B(...["20"]).valueOf());
        trace = "";
        function* broken() { trace += "b"; yield input; trace += "t"; throw "spread"; }
        try { new C(...broken()); }
        catch (error) { console.log(trace, error === "spread"); }
        """;

    public const string SuspendedSource = """
        function* make(C: any) {
            yield 0;
            return new C("15");
        }
        const iterator: any = make(Number);
        iterator.next();
        const generated: any = iterator.next().value;
        console.log("generator", typeof generated, generated.valueOf());
        async function run(C: any) {
            await Promise.resolve(0);
            const value: any = new C("14");
            const omitted: any = new C(), undef: any = new C(undefined);
            console.log("async", typeof value, value.valueOf(), omitted.valueOf(),
                Number.isNaN(undef.valueOf()));
        }
        run(Number);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "object 7\n"];
        yield return ["coercion", CoercionSource,
            "object 0 true 0\n7 8 1 true\n9 true true true\ntrue\n"];
        yield return ["runtime", RuntimeSource, "cabv object 13\n0 1\ntrue\n"];
        yield return ["bound", BoundSource, "object 7\n11 0 true 11\n"];
        yield return ["spread", SpreadSource, "stev object 17\n0 true\n18 19\nbt true\n"];
        yield return ["suspended", SuspendedSource, "generator object 15\nasync object 14 0 true\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void ConstructorValuesCreateFreshNumberWrappers(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
