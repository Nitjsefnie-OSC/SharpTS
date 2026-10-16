using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ReflectSymbolKeyTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", """
            const object: any = {}, key = Symbol("key");
            Object.defineProperty(object, key, { value: 3, writable: true, configurable: true });
            console.log(Reflect.get(object, key));
            console.log(Reflect.set(object, key, 4), object[key], object["Symbol(key)"]);
            """, "3\ntrue 4 undefined\n"];
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
        yield return ["receiver-presence", """
            "use strict";
            for (const key of ["accessor", Symbol("accessor")]) {
                const target: any = {}, state: any = {};
                Object.defineProperty(target, key, {
                    get: function(this: any) { return this; },
                    set: function(this: any, value: any) { state.receiver = this; state.value = value; }
                });
                console.log(Reflect.get(target, key) === target,
                    Reflect.get(target, key, undefined) === undefined, Reflect.get(target, key, null) === null);
                console.log(Reflect.set(target, key, 1), state.receiver === target, state.value);
                console.log(Reflect.set(target, key, 2, undefined), state.receiver === undefined, state.value);
                console.log(Reflect.set(target, key, 3, null), state.receiver === null, state.value);
            }
            """, "true true true\ntrue true 1\ntrue true 2\ntrue true 3\ntrue true true\ntrue true 1\ntrue true 2\ntrue true 3\n"];
        yield return ["receiver-data-presence", """
            for (const key of ["accessor", Symbol("accessor")]) {
                const data: any = {};
                Object.defineProperty(data, key, { value: 7, writable: true });
                console.log(Reflect.get(data, key, undefined), Reflect.get(data, key, null));
                console.log(Reflect.set(data, key, 8), Reflect.set(data, key, 9, undefined),
                    Reflect.set(data, key, 10, null), data[key]);
            }
            """, "7 7\ntrue false false 8\n7 7\ntrue false false 8\n"];
        yield return ["proxy", """
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
            """, "7\ntrue 8\ntrue true\n8\ntrue true\ntrue 9 1 1\n"];
        yield return ["proxy-receiver", """
            "use strict";
            const key = Symbol("receiver"), other = Symbol("receiver"), target: any = {}, backing: any = {};
            const state = { reads: 0, writes: 0, correct: true };
            Object.defineProperty(target, key, { value: 3, writable: true });
            const receiver: any = new Proxy(new Proxy(backing, {
                getOwnPropertyDescriptor(t: any, property: any) {
                    state.reads++; state.correct = state.correct && property === key && property !== other;
                    return Object.getOwnPropertyDescriptor(t, property);
                },
                defineProperty(t: any, property: any, descriptor: any) {
                    state.writes++; state.correct = state.correct && property === key && property !== other;
                    Object.defineProperty(t, property, descriptor);
                    return true;
                }
            }), {});
            console.log(Reflect.set(target, key, 4, receiver), target[key], backing[key], backing[other]);
            console.log(state.reads, state.writes, state.correct);
            const accessor: any = {};
            Object.defineProperty(accessor, key, {
                get: function(this: any) { return this; },
                set: function(this: any, value: any) { backing.receiver = this; backing.value = value; }
            });
            const forwarding: any = new Proxy(new Proxy(accessor, {}), {});
            console.log(Reflect.get(forwarding, key) === forwarding,
                Reflect.get(forwarding, key, undefined) === undefined, Reflect.get(forwarding, key, null) === null);
            console.log(Reflect.set(forwarding, key, 5, undefined), backing.receiver === undefined, backing.value);
            console.log(Reflect.set(forwarding, key, 6, null), backing.receiver === null, backing.value);
            """, "true 3 4 undefined\n1 1 true\ntrue true true\ntrue true 5\ntrue true 6\n"];
        yield return ["proxy-invariants", """
            const key = Symbol("fixed"), target: any = {};
            Object.defineProperty(target, key, { value: 3, writable: false, configurable: false });
            const badGet: any = new Proxy(target, { get() { return 4; } });
            const set: any = new Proxy(target, { set() { return true; } });
            try { Reflect.get(badGet, key); } catch (error) { console.log(error instanceof TypeError); }
            try { Reflect.set(set, key, 4); } catch (error) { console.log(error instanceof TypeError); }
            console.log(Reflect.get(new Proxy(target, { get() { return 3; } }), key), Reflect.set(set, key, 3));
            const accessor: any = {};
            Object.defineProperty(accessor, key, { get: undefined, set: undefined, configurable: false });
            const trapping: any = new Proxy(accessor, { get() { return 1; }, set() { return true; } });
            try { Reflect.get(trapping, key); } catch (error) { console.log(error instanceof TypeError); }
            try { Reflect.set(trapping, key, 1); } catch (error) { console.log(error instanceof TypeError); }
            """, "true\ntrue\n3 true\ntrue\ntrue\n"];
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

    public static IEnumerable<object[]> ModeCases()
    {
        foreach (var row in Cases())
        {
            // Interpreter Symbol-producing key coercion is tracked in #2354.
            // Class method lookup retains its existing compiler-only coverage.
            if ((string)row[0] is not ("coercion" or "coercion-errors" or "coercion-order" or "class-methods"))
                yield return [row[0], row[1], row[2], ExecutionMode.Interpreted];
            // Compiled null data receivers fail in descriptor lookup (#2356).
            if ((string)row[0] != "receiver-data-presence")
                yield return [row[0], row[1], row[2], ExecutionMode.Compiled];
        }
    }

    [Theory, MemberData(nameof(ModeCases))]
    public void ReflectPreservesSymbolKeysAndReceivers(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
