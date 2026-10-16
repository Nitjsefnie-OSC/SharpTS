using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class NullRootInstanceofTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original-and-intrinsic", """
            const root: any = Object.create(null);
            const nested: any = Object.create(root);
            const native: any = new Map();
            const nativeChild: any = Object.create(native);
            console.log(root instanceof Object, nested instanceof Object, native instanceof Object, nativeChild instanceof Object);
            const hasInstance: any = Function.prototype[Symbol.hasInstance];
            console.log(Reflect.apply(hasInstance, Object, [root]), Reflect.apply(hasInstance, Object, [nested]),
                Reflect.apply(hasInstance, Object, [native]), Reflect.apply(hasInstance, Object, [nativeChild]));
            """, "false false true true\nfalse false true true\n"];
        yield return ["ordinary-and-native-positive-controls", """
            class C {}
            function F() {}
            for (const value of [{}, Object.create({}), [], new Map(), new Set(), new Date(), /x/, Promise.resolve(1), new C(), F]) {
                console.log(value instanceof Object);
            }
            """, "true\ntrue\ntrue\ntrue\ntrue\ntrue\ntrue\ntrue\ntrue\ntrue\n"];
        yield return ["primitives", """
            const hasInstance: any = Function.prototype[Symbol.hasInstance];
            for (const value of [null, undefined, 1, "x", true, Symbol("x"), 1n]) {
                console.log((value as any) instanceof Object, Reflect.apply(hasInstance, Object, [value]));
            }
            """, "false false\nfalse false\nfalse false\nfalse false\nfalse false\nfalse false\nfalse false\n"];
        yield return ["native-compatibility", """
            const formatter: any = new Intl.NumberFormat("en-US");
            const readable: any = new ReadableStream();
            const writable: any = new WritableStream();
            const transform: any = new TransformStream();
            console.log(formatter instanceof Object, readable instanceof Object, writable instanceof Object, transform instanceof Object);
            console.log(new TextEncoder() instanceof Object, new TextDecoder() instanceof Object);
            """, "true true true true\ntrue true\n"];
        yield return ["explicit-null-array-and-callable-chains", """
            const array: any = [1];
            function F() {}
            const fn: any = F;
            const Bound: any = fn.bind(null);
            Object.setPrototypeOf(array, null);
            Object.setPrototypeOf(fn, null);
            Object.setPrototypeOf(Bound, null);
            console.log(array instanceof Object, fn instanceof Object, Bound instanceof Object);
            console.log(fn instanceof Function, Bound instanceof Function);
            Object.setPrototypeOf(array, Array.prototype);
            Object.setPrototypeOf(fn, Function.prototype);
            Object.setPrototypeOf(Bound, Function.prototype);
            console.log(array instanceof Object, fn instanceof Object, Bound instanceof Object);
            console.log(fn instanceof Function, Bound instanceof Function);
            """, "false false false\nfalse false\ntrue true true\ntrue true\n"];
        yield return ["intrinsic-roots", """
            console.log(Object.prototype instanceof Object);
            console.log(Function.prototype instanceof Function, Function.prototype instanceof Object, Function instanceof Function);
            console.log(Object.create(Object.prototype) instanceof Object);
            """, "false\nfalse true true\ntrue\n"];
        yield return ["date-prototype-parent", """
            console.log(Object.getPrototypeOf(Date.prototype) === Date.prototype);
            console.log(Object.getPrototypeOf(Date.prototype) === Object.prototype, Date.prototype instanceof Object);
            """, "false\ntrue true\n"];
        yield return ["alternate-class-prototype", """
            class C {}
            function NewTarget() {}
            const target: any = NewTarget;
            const root: any = Object.create(null);
            target.prototype = root;
            const value: any = Reflect.construct(C, [], target);
            console.log(value instanceof Object, Object.getPrototypeOf(value) === root);
            console.log(Function.prototype[Symbol.hasInstance].call(Object, value));
            """, "false true\nfalse\n"];
        yield return ["proxy-prototype-traps", """
            let calls = 0;
            const root: any = Object.create(null);
            const proxy: any = new Proxy(root, {
                getPrototypeOf(target: any) { calls++; return null; }
            });
            console.log(proxy instanceof Object, calls);
            const native: any = new Proxy(new Map(), { getPrototypeOf(target: any) { return null; } });
            console.log(native instanceof Object, Object.create(native) instanceof Object);
            const forwarding: any = new Proxy(new Map(), {});
            console.log(forwarding instanceof Object);
            const ordinary: any = new Proxy({}, {
                getPrototypeOf(target: any) { return Object.prototype; }
            });
            console.log(ordinary instanceof Object);
            const token = {};
            const bad: any = new Proxy({}, { getPrototypeOf(target: any) { throw token; } });
            try { console.log(bad instanceof Object); } catch (error) { console.log(error === token); }
            """, "false 1\nfalse false\ntrue\ntrue\ntrue\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void InstanceofUsesEffectivePrototypeMembership(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, output) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            Assert.Equal(expected, output);
        }
        else
        {
            Assert.Equal(expected, TestHarness.Run(source, mode));
        }
    }
}
