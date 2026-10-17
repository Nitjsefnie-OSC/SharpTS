using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class CustomHasInstanceTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["bound-class", """
            const token: any = {};
            let calls = 0;
            class C {
                static [Symbol.hasInstance](value: any) {
                    calls++;
                    console.log(this === C);
                    return value === token;
                }
            }
            const target: any = C, Bound: any = target.bind({}), Twice: any = Bound.bind({});
            console.log(token instanceof target, token instanceof Twice);
            console.log(calls);
            """, "true\ntrue\ntrue true\n2\n"];
        yield return ["accessors", """
            const matcher: any = {};
            let reads = 0;
            const handler = function(value: any) {
                console.log(this === matcher, value === null);
                return value === null ? "accepted" : 0;
            };
            Object.defineProperty(matcher, Symbol.hasInstance, {
                get: function() { reads++; console.log(this === matcher); return handler; }
            });
            console.log(null instanceof matcher, 1 instanceof matcher);
            console.log(reads);
            """, "true\ntrue true\ntrue\ntrue false\ntrue false\n2\n"];
        yield return ["inherited", """
            class Base { static [Symbol.hasInstance](value: any) { return this === Derived && value === null; } }
            class Derived extends Base {}
            const target: any = Derived, Twice: any = target.bind({}).bind({});
            console.log(null instanceof target, null instanceof Twice, {} instanceof target);
            const prototype: any = { [Symbol.hasInstance]: function(value: any) { return this === matcher && value === 7; } };
            const matcher: any = Object.create(prototype);
            console.log(7 instanceof matcher, 8 instanceof matcher);
            """, "true false false\ntrue false\n"];
        yield return ["errors", """
            const token: any = {};
            const lookup: any = {};
            Object.defineProperty(lookup, Symbol.hasInstance, { get() { throw token; } });
            try { console.log(null instanceof lookup); } catch (error) { console.log(error === token); }
            const invocation: any = { [Symbol.hasInstance]: () => { throw token; } };
            try { console.log({} instanceof invocation); } catch (error) { console.log(error === token); }
            const invalid: any = { [Symbol.hasInstance]: 1 };
            try { console.log(null instanceof invalid); } catch (error) { console.log(error instanceof TypeError); }
            """, "true\ntrue\ntrue\n"];
        yield return ["proxy", """
            const matcher: any = new Proxy({}, {
                get(target: any, key: any, receiver: any) {
                    console.log(key === Symbol.hasInstance, receiver === matcher);
                    return function(value: any) { console.log(this === matcher); return value === null; };
                }
            });
            console.log(null instanceof matcher);
            """, "true true\ntrue\ntrue\n"];
        yield return ["ordinary", """
            function C(this: any) {}
            const Bound: any = C.bind({}), value: any = new Bound();
            console.log(value instanceof C, value instanceof Bound, null instanceof Bound);
            const bind: any = Function.prototype.bind;
            const A: any = Reflect.apply(bind, Array, [{}]);
            const M: any = Reflect.apply(bind, Map, [{}]);
            const O: any = Reflect.apply(bind, Object, [{}]);
            console.log([] instanceof A, new Map() instanceof M, {} instanceof O);
            console.log([] instanceof Array, new Map() instanceof Map, {} instanceof Object,
                new Number(1) instanceof Number, new String("x") instanceof String,
                new Boolean(false) instanceof Boolean);
            Object.defineProperty(C, Symbol.hasInstance, { value: null });
            console.log(value instanceof C);
            """, "true true false\ntrue true true\ntrue true true true true true\ntrue\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    public static IEnumerable<object[]> StandaloneCases()
        // Proxy construction uses the optional SharpTS runtime dependency;
        // the dual-mode cases above exercise its handler dispatch.
        => Cases().Where(row => (string)row[0] != "proxy");

    [Theory, MemberData(nameof(ParityCases))]
    public void InstanceofUsesCustomHandlerBeforeOrdinaryChecks(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void InheritedProxyHandlerLookupPreservesOriginalReceiver(ExecutionMode mode)
    {
        const string source = """
            const prototype: any = new Proxy({}, {
                get(target: any, key: any, receiver: any) {
                    console.log(key === Symbol.hasInstance, receiver === matcher);
                    return function(value: any) { console.log(this === matcher); return value === null; };
                }
            });
            const matcher: any = Object.create(prototype);
            console.log(null instanceof matcher);
            """;
        Assert.Equal("true true\ntrue\ntrue\n", TestHarness.Run(source, mode));
    }
}
