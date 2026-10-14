using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class InvalidInstanceofTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["right-operands", """
            const targets: any[] = [null, undefined, 1, "x", true, 1n, Symbol(), {}, [], /x/, new Date()];
            for (const target of targets) {
                try { console.log({} instanceof target); }
                catch (error: any) { console.log(error.name, error instanceof TypeError); }
                try { console.log(null instanceof target); }
                catch (error: any) { console.log(error.name, error instanceof TypeError); }
            }
            """, string.Concat(Enumerable.Repeat("TypeError true\n", 22))];
        yield return ["function-prototypes", """
            function C() {}
            const target: any = C, bound: any = target.bind({}).bind({});
            for (const prototype of [null, undefined, 1, "x", false, 1n, Symbol()]) {
                target.prototype = prototype;
                console.log(null instanceof bound, 1 instanceof bound);
                try { console.log({} instanceof target); }
                catch (error: any) { console.log(error instanceof TypeError); }
                try { console.log({} instanceof bound); }
                catch (error: any) { console.log(error instanceof TypeError); }
            }
            """, string.Concat(Enumerable.Repeat("false false\ntrue\ntrue\n", 7))];
        yield return ["callable-prototypes", """
            class C { method() {} }
            async function asynchronous() {}
            const instance: any = new C();
            const targets: any[] = [() => 1, asynchronous, instance.method];
            for (const target of targets) {
                console.log(null instanceof target, 1 instanceof target);
                try { console.log({} instanceof target); }
                catch (error: any) { console.log(error instanceof TypeError); }
            }
            const arrow: any = () => 1;
            arrow.prototype = {};
            console.log(Object.create(arrow.prototype) instanceof arrow, {} instanceof arrow);
            """, string.Concat(Enumerable.Repeat("false false\ntrue\n", 3)) + "true false\n"];
        yield return ["prototype-getter", """
            const token: any = {}, target: any = () => 1;
            let reads = 0;
            Object.defineProperty(target, "prototype", { get() { reads++; throw token; } });
            console.log(null instanceof target, undefined instanceof target, 1 instanceof target);
            try { console.log({} instanceof target); }
            catch (error) { console.log(error === token); }
            console.log(reads);
            """, "false false false\ntrue\n1\n"];
        yield return ["valid-controls", """
            function C() {}
            const target: any = C;
            Object.defineProperty(target, Symbol.hasInstance, { value: undefined });
            console.log(new C() instanceof target, {} instanceof target);
            const matcher: any = { [Symbol.hasInstance]: () => true };
            console.log(null instanceof matcher, 1 instanceof matcher);
            console.log({} instanceof Object, [] instanceof Array, new Map() instanceof Map);
            console.log(new AbortController().signal instanceof (AbortSignal as any), {} instanceof (AbortSignal as any));
            function* generator() {}
            async function* asyncGenerator() {}
            const sync: any = generator, async: any = asyncGenerator;
            console.log({} instanceof sync, {} instanceof async);
            """, "true false\ntrue true\ntrue true true\ntrue false\nfalse false\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void InvalidInstanceofOperandsThrowGuestTypeErrors(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
