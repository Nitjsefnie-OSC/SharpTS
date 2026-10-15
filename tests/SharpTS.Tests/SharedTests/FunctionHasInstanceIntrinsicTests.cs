using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class FunctionHasInstanceIntrinsicTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["bound", """
            function C() {}
            const target: any = C, Bound: any = target.bind(), Twice: any = Bound.bind();
            const value: any = new Bound();
            console.log(typeof Function.prototype[Symbol.hasInstance]);
            console.log(Bound[Symbol.hasInstance](value), Twice[Symbol.hasInstance](value));
            console.log(value instanceof C, value instanceof Bound, value instanceof Twice);
            """, "function\ntrue true\ntrue true true\n"];
        yield return ["descriptor", """
            const prototype: any = Function.prototype, key = Symbol.hasInstance;
            const intrinsic: any = prototype[key];
            const descriptor: any = Object.getOwnPropertyDescriptor(prototype, key);
            const reflected: any = Reflect.getOwnPropertyDescriptor(prototype, key);
            console.log(descriptor.value === intrinsic, reflected.value === intrinsic,
                descriptor.writable, descriptor.enumerable, descriptor.configurable);
            console.log(intrinsic.name, intrinsic.length, Object.hasOwn(intrinsic, "prototype"));
            console.log(Object.getOwnPropertySymbols(prototype).includes(key), Reflect.ownKeys(prototype).includes(key));
            try { new intrinsic(); } catch (error) { console.log(error instanceof TypeError); }
            """, "true true false false false\n[Symbol.hasInstance] 1 false\ntrue true\ntrue\n"];
        yield return ["ordinary", """
            const ordinary: any = Function.prototype[Symbol.hasInstance];
            function C() {}
            const Constructor: any = C, value = new Constructor();
            console.log(ordinary.call(C, value), ordinary.apply(C, [{}]));
            console.log(ordinary(), ordinary.call(C), ordinary.call(C, value, 99));
            console.log(ordinary.call({}, {}), ordinary.call(null, {}),
                ordinary.call(undefined, {}), ordinary.call(1, {}), ordinary.call("x", {}));
            Constructor.prototype = 1;
            console.log(ordinary.call(C, 1));
            try { ordinary.call(C, {}); } catch (error) { console.log(error instanceof TypeError); }
            const arrow: any = () => {};
            console.log(ordinary.call(arrow, null));
            try { ordinary.call(arrow, {}); } catch (error) { console.log(error instanceof TypeError); }
            arrow.prototype = {};
            console.log(ordinary.call(arrow, Object.create(arrow.prototype)));
            """, "true false\nfalse false true\nfalse false false false false\nfalse\ntrue\nfalse\ntrue\ntrue\n"];
        yield return ["hooks", """
            const ordinary: any = Function.prototype[Symbol.hasInstance];
            function C() {}
            const Constructor: any = C, value: any = new Constructor();
            let calls = 0;
            Object.defineProperty(C, Symbol.hasInstance, {
                value: function(candidate: any) { calls++; console.log(this === C); return candidate === null; }
            });
            console.log(ordinary.call(C, value), ordinary.call(C, null), calls);
            const Bound: any = Constructor.bind(null), Twice: any = Bound.bind(null);
            console.log(ordinary.call(Bound, null), ordinary.call(Twice, null), calls);
            console.log((null as any) instanceof Constructor, calls);
            """, "true false 0\ntrue\ntrue\ntrue true 2\ntrue\ntrue 3\n"];
        yield return ["variants", """
            const ordinary: any = Function.prototype[Symbol.hasInstance];
            class C {}
            const Expression: any = class {};
            const fn: any = function() {}, arrow: any = () => {};
            function* generator() { yield 1; }
            async function* asyncGenerator() { yield 1; }
            console.log(C[Symbol.hasInstance] === ordinary, Expression[Symbol.hasInstance] === ordinary,
                fn[Symbol.hasInstance] === ordinary, arrow[Symbol.hasInstance] === ordinary,
                generator[Symbol.hasInstance] === ordinary, asyncGenerator[Symbol.hasInstance] === ordinary);
            console.log(C[Symbol.hasInstance](new C()), Expression[Symbol.hasInstance](new Expression()));
            console.log(ordinary.call(Number, new Number(1)), ordinary.call(String, new String("x")),
                ordinary.call(Boolean, new Boolean(false)), ordinary.call(Array, []), ordinary.call(Object, {}));
            """, "true true true true true true\ntrue true\ntrue true true true true\n"];
        yield return ["readonly", """
            "use strict";
            const prototype: any = Function.prototype, key = Symbol.hasInstance;
            const ordinary: any = prototype[key], replacement = () => false;
            function C() {}
            class D {}
            const target: any = C, classTarget: any = D;
            const targets: any[] = [target, classTarget, target.bind(null), classTarget.bind(null), Number];
            console.log(Reflect.set(prototype, key, replacement));
            try { delete prototype[key]; } catch (error) { console.log(error instanceof TypeError); }
            for (const constructor of targets) {
                console.log(Reflect.set(constructor, key, replacement), Object.hasOwn(constructor, key));
                try { constructor[key] = replacement; } catch (error) { console.log(error instanceof TypeError); }
                console.log(constructor[key] === ordinary);
            }
            try { Object.defineProperty(prototype, key, { value: replacement }); }
            catch (error) { console.log(error instanceof TypeError); }
            console.log(prototype[key] === ordinary, target[key] === ordinary);
            """, "false\ntrue\n" + string.Concat(Enumerable.Repeat("false false\ntrue\ntrue\n", 5)) + "true\ntrue true\n"];
        yield return ["proxy", """
            const ordinary: any = Function.prototype[Symbol.hasInstance], events: string[] = [];
            function C() {}
            const Constructor: any = C;
            const ProxyConstructor: any = new Proxy(C, {
                get(target: any, key: any, receiver: any) {
                    if (key === "prototype") events.push("prototype");
                    return Reflect.get(target, key, receiver);
                }
            });
            console.log(ordinary.call(ProxyConstructor, 1), events.length);
            console.log(ordinary.call(ProxyConstructor, new Constructor()), events.join(","));
            const candidate: any = new Proxy({}, {
                getPrototypeOf() { events.push("candidate"); return Constructor.prototype; }
            });
            console.log(ordinary.call(C, candidate), events.join(","));
            """, "false 0\ntrue prototype\ntrue prototype,candidate\n"];
        yield return ["hook-writes", """
            class Base { static [Symbol.hasInstance](value: any) { return false; } }
            class Derived extends Base {}
            const target: any = Derived;
            target[Symbol.hasInstance] = function() { return true; };
            console.log({} instanceof target, Object.hasOwn(target, Symbol.hasInstance), {} instanceof Base);
            class Generic<T> {}
            const generic: any = Generic;
            Object.defineProperty(generic, Symbol.hasInstance, {
                value: () => true, writable: true, configurable: true
            });
            generic[Symbol.hasInstance] = () => false;
            console.log({} instanceof generic, Object.hasOwn(generic, Symbol.hasInstance));
            let writes = 0;
            class Setter<T> { static set [Symbol.hasInstance](value: any) { writes++; } }
            const setter: any = Setter;
            setter[Symbol.hasInstance] = () => true;
            console.log(writes);
            let reads = 0;
            const handler = (value: any) => value === null;
            class Getter { static get [Symbol.hasInstance]() { reads++; return handler; } }
            const getter: any = Getter;
            console.log((null as any) instanceof getter, reads);
            console.log((null as any) instanceof getter, reads);
            """, "true true false\nfalse true\n1\ntrue 1\ntrue 2\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void IntrinsicUsesOrdinaryInstanceSemantics(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
