using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ReflectConstructOrdinaryPrototypeTests
{
    [Theory, ModeData]
    public void PrototypeIsReadBeforeBodyEvenWhenBodyReturnsAnObject(ExecutionMode mode)
    {
        const string source = """
            const Target: any = function () { console.log("body"); return { own: 1 }; };
            const Other: any = function () {};
            const newTarget: any = new Proxy(Other, {
                get(target: any, key: any) {
                    if (key === "prototype") console.log("prototype");
                    return target[key];
                }
            });
            const created: any = Reflect.construct(Target, [], newTarget);
            console.log("result", created.own);
            for (const bad of [null, undefined]) {
                try { Reflect.construct(Target, [], bad as any); console.log("accepted"); }
                catch (error) { console.log(error instanceof TypeError); }
            }
            """;
        Assert.Equal("prototype\nbody\nresult 1\ntrue\ntrue\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void AlternatePrototypeIsInstalledBeforeTheConstructorBody(ExecutionMode mode)
    {
        const string source = """
            let observed: any;
            const Plain: any = function () { observed = Object.getPrototypeOf(this); this.own = 7; };
            const Other: any = function () {};
            const instance: any = Reflect.construct(Plain, [], Other);
            console.log(observed === Other.prototype, Object.getPrototypeOf(instance) === Other.prototype,
                instance instanceof Plain, instance instanceof Other, instance.own);
            """;
        Assert.Equal("true true false true 7\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void AbruptPrototypeGetterPreventsBodyExecution(ExecutionMode mode)
    {
        const string source = """
            let calls = 0;
            const Target: any = function () { calls++; return {}; };
            const Other: any = function () {};
            const failure = { marker: 1 };
            const alternate: any = new Proxy(Other, {
                get(target: any, key: any) {
                    if (key === "prototype") throw failure;
                    return target[key];
                }
            });
            try { Reflect.construct(Target, [], alternate); }
            catch (error) { console.log(error === failure); }
            console.log(calls);
            """;
        Assert.Equal("true\n0\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void PrimitivePrototypeFallsBackToObjectPrototype(ExecutionMode mode)
    {
        const string source = """
            const Target: any = function () {};
            const Other: any = function () {};
            for (const prototype of [null, undefined, 1, "text", false]) {
                Other.prototype = prototype;
                const instance: any = Reflect.construct(Target, [], Other);
                console.log(Object.getPrototypeOf(instance) === Object.prototype);
            }
            """;
        Assert.Equal("true\ntrue\ntrue\ntrue\ntrue\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void BoundAndProxyTargetsPreserveAnAlternateNewTarget(ExecutionMode mode)
    {
        const string source = """
            const Target: any = function (value: number) { this.value = value; };
            const Other: any = function () {};
            const Bound: any = Target.bind({ value: 99 }, 7);
            const ProxyTarget: any = new Proxy(Target, {});
            const bound: any = Reflect.construct(Bound, [], Other);
            const proxied: any = Reflect.construct(ProxyTarget, [9], Other);
            console.log(Object.getPrototypeOf(bound) === Other.prototype, bound.value,
                Object.getPrototypeOf(proxied) === Other.prototype, proxied.value);
            """;
        Assert.Equal("true 7 true 9\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void CapturedConstructDistinguishesOmissionFromExplicitNullishNewTarget(ExecutionMode mode)
    {
        const string source = """
            const construct: any = Reflect.construct;
            const Target: any = function () {};
            console.log(construct(Target, []) instanceof Target);
            for (const invalid of [null, undefined]) {
                try { construct(Target, [], invalid); console.log(false); }
                catch (error) { console.log(error instanceof TypeError); }
            }
            """;
        Assert.Equal("true\ntrue\ntrue\n", TestHarness.Run(source, mode));
    }
}
