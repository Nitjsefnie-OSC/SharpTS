using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ReflectSymbolKeyTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["data", """
            const key = Symbol("key"), other = Symbol("key"), constructor: any = Number;
            for (const target of [{}, constructor]) {
                Object.defineProperty(target, key, { value: 3, writable: true, configurable: true });
                Object.defineProperty(target, other, { value: 9, writable: true, configurable: true });
                target[key.toString()] = 11;
                console.log(Reflect.get(target, key), Reflect.get(target, other));
                console.log(Reflect.set(target, key, 4), target[key], target[other], target[key.toString()]);
                const get: any = Reflect.get, set: any = Reflect.set;
                console.log(get(target, key), set(target, other, 10), get(target, other));
            }
            """, "3 9\ntrue 4 9 11\n4 true 10\n3 9\ntrue 4 9 11\n4 true 10\n"];
        yield return ["accessors", """
            const key = Symbol("accessor"), target: any = {}, receiver: any = { slot: 7 };
            Object.defineProperty(target, key, {
                get: function(this: any) { return this.slot; },
                set: function(this: any, value: any) { this.slot = value; },
                configurable: true
            });
            console.log(Reflect.get(target, key, receiver));
            console.log(Reflect.set(target, key, 8, receiver), receiver.slot, Object.hasOwn(receiver, key));
            const child: any = Object.create(target);
            child.slot = 9;
            console.log(Reflect.get(child, key), Reflect.set(child, key, 10), child.slot, receiver.slot);
            const get: any = Reflect.get, set: any = Reflect.set;
            console.log(get(child, key, receiver), set(child, key, 12, receiver), receiver.slot);
            """, "7\ntrue 8 false\n9 true 10 8\n8 true 12\n"];
        yield return ["receiver-data", """
            const key = Symbol("data"), target: any = {}, receiver: any = {};
            Object.defineProperty(target, key, { value: 3, writable: true, configurable: true });
            console.log(Reflect.set(target, key, 4, receiver), target[key], receiver[key]);
            const descriptor = Object.getOwnPropertyDescriptor(receiver, key)!;
            console.log(descriptor.writable, descriptor.enumerable, descriptor.configurable);
            console.log(Reflect.set(target, key, 5, receiver), target[key], receiver[key]);
            Object.defineProperty(target, key, { writable: false });
            console.log(Reflect.set(target, key, 6, receiver), receiver[key]);
            const setterOnly = Symbol("setter");
            Object.defineProperty(target, setterOnly, { set: function(value: any) {} });
            console.log(Reflect.get(target, setterOnly) === undefined);
            """, "true 3 4\ntrue true true\ntrue 3 5\nfalse 5\ntrue\n"];
        yield return ["coercion", """
            const key = Symbol("coerced"), target: any = {}, keyObject: any = {};
            let reads = 0, hint = "";
            keyObject[Symbol.toPrimitive] = function(value: string) { reads++; hint = value; return key; };
            Object.defineProperty(target, key, { value: 3, writable: true, configurable: true });
            console.log(Reflect.get(target, keyObject), reads, hint);
            console.log(Reflect.set(target, keyObject, 4), target[key], reads, hint);
            const get: any = Reflect.get, set: any = Reflect.set;
            console.log(get(target, keyObject), set(target, keyObject, 5), target[key], reads);
            console.log(Reflect.get({ "7": 8 }, 7));
            """, "3 1 string\ntrue 4 2 string\n4 true 5 4\n8\n"];
        yield return ["coercion-errors", """
            const key = Symbol("fallback"), target: any = {}, fallback: any = {};
            let order = "";
            Object.defineProperty(target, key, { value: 7 });
            fallback.toString = function() { order += "S"; return {}; };
            fallback.valueOf = function() { order += "V"; return key; };
            console.log(Reflect.get(target, fallback), order);
            const bad: any = {}, returnsObject: any = {}, throws: any = {}, marker: any = {};
            bad[Symbol.toPrimitive] = 1;
            returnsObject[Symbol.toPrimitive] = function() { return {}; };
            throws[Symbol.toPrimitive] = function() { throw marker; };
            for (const value of [bad, returnsObject]) {
                try { Reflect.get(target, value); }
                catch (error) { console.log(error instanceof TypeError); }
            }
            try { Reflect.get(target, throws); }
            catch (error) { console.log(error === marker); }
            """, "7 SV\ntrue\ntrue\ntrue\n"];
        yield return ["class-methods", """
            const key = Symbol("method");
            class Base { static [key]() { return this; } }
            class Child extends Base {}
            class Own extends Base { static [key]() { return this; } }
            const base: any = Base, child: any = Child, own: any = Own;
            const method: any = Reflect.get(base, key), inherited: any = Reflect.get(child, key);
            console.log(typeof method, typeof inherited, method === inherited);
            console.log(method.call(base) === base, inherited.call(child) === child);
            base[key] = null;
            console.log(Reflect.get(base, key), Reflect.get(child, key));
            const overridden: any = Reflect.get(own, key);
            console.log(typeof overridden, overridden.call(own) === own, overridden === own[key]);
            """, "function function true\ntrue true\nnull null\nfunction true true\n"];
        yield return ["coercion-order", """
            const key = Symbol("inherited"), target: any = {}, prototype: any = {};
            const propertyKey: any = Object.create(prototype);
            let order = "", correctReceiver = true;
            Object.defineProperty(target, key, { value: 7 });
            Object.defineProperty(prototype, Symbol.toPrimitive, {
                get: function(this: any) {
                    order += "G"; correctReceiver = correctReceiver && this === propertyKey;
                    return function(this: any, hint: string) {
                        order += "C"; correctReceiver = correctReceiver && this === propertyKey && hint === "string";
                        return key;
                    };
                }
            });
            function receiver() { order += "R"; return {}; }
            console.log(Reflect.get(target, propertyKey, receiver()), order, correctReceiver);
            """, "7 RGC true\n"];
    }

    [Theory, ModeData]
    public void ReflectPreservesNumberConstructorSymbolKeys(ExecutionMode mode)
    {
        const string source = """
            const key = Symbol("key"), constructor: any = Number;
            Object.defineProperty(constructor, key, { value: 3, writable: true, configurable: true });
            console.log(Reflect.get(constructor, key));
            console.log(Reflect.set(constructor, key, 4), constructor[key]);
            """;
        Assert.Equal("3\ntrue 4\n", TestHarness.Run(source, mode));
    }

    [Theory, MemberData(nameof(Cases))]
    public void CompiledReflectPreservesSymbolKeysAndReceivers(
        string name, string source, string expected)
    {
        // Interpreter ordinary-object Symbol dispatch is tracked separately in #2159.
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.RunCompiled(source));
    }

    [Fact]
    public void CompiledReflectPreservesSymbolKeysThroughProxyTrapsAndForwarding()
    {
        // Proxy execution uses the hosted runtime; interpreter Symbol dispatch is #2159.
        const string source = """
            const key = Symbol("proxy"), target: any = {}, receiver: any = { slot: 7 };
            Object.defineProperty(target, key, {
                get: function(this: any) { return this.slot; },
                set: function(this: any, value: any) { this.slot = value; },
                configurable: true
            });
            const forwarding: any = new Proxy(new Proxy(target, {}), {});
            console.log(Reflect.get(forwarding, key, receiver));
            console.log(Reflect.set(forwarding, key, 8, receiver), receiver.slot);
            let gets = 0, sets = 0;
            const trapping: any = new Proxy(target, {
                get(t: any, property: any, r: any) {
                    gets++; console.log(property === key, r === receiver);
                    return Reflect.get(t, property, r);
                },
                set(t: any, property: any, value: any, r: any) {
                    sets++; console.log(property === key, r === receiver);
                    return Reflect.set(t, property, value, r);
                }
            });
            console.log(Reflect.get(trapping, key, receiver));
            console.log(Reflect.set(trapping, key, 9, receiver), receiver.slot, gets, sets);
            """;
        Assert.Equal("7\ntrue 8\ntrue true\n8\ntrue true\ntrue 9 1 1\n", TestHarness.RunCompiled(source));
    }
}
