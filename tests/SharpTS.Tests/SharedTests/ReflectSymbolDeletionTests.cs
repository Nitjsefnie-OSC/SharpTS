using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ReflectSymbolDeletionTests
{
    public const string IssueSource = """
        const object: any = {}, key = Symbol("key");
        Object.defineProperty(object, key, { value: 3, configurable: true });
        console.log(Reflect.deleteProperty(object, key), Object.getOwnPropertySymbols(object).length);
        """;

    public static IEnumerable<object[]> OrdinaryCases()
    {
        yield return ["issue", IssueSource, "true 0\n"];
        yield return ["identity-and-absence", """
            const key = Symbol("same"), other = Symbol("same"), target: any = {};
            Object.defineProperty(target, key, { value: 3, configurable: true });
            Object.defineProperty(target, other, { value: 8, configurable: true });
            target["Symbol(same)"] = 9;
            const remove: any = Reflect.deleteProperty;
            console.log(remove(target, key), target[key] === undefined, target[other],
                target["Symbol(same)"], Object.getOwnPropertySymbols(target).length);
            console.log(Reflect.deleteProperty(target, key),
                Reflect.deleteProperty(target, Symbol("same")), target[other]);
            console.log(remove(target, other), Object.getOwnPropertySymbols(target).length,
                target["Symbol(same)"]);
            const prototype: any = {};
            Object.defineProperty(prototype, key, { value: 7, configurable: false });
            const child: any = Object.create(prototype);
            console.log(Reflect.deleteProperty(child, key), child[key], prototype[key],
                Object.hasOwn(child, key), Reflect.deleteProperty(prototype, key));
            """, "true true 8 9 1\ntrue true 8\ntrue 0 9\ntrue 7 7 false false\n"];
        yield return ["descriptors-and-integrity", """
            const key = Symbol("locked"), target: any = {}, state = { gets: 0 };
            Object.defineProperty(target, key, { value: 3, configurable: false });
            console.log(Reflect.deleteProperty(target, key), target[key], Object.hasOwn(target, key));
            const accessor = Symbol("accessor");
            Object.defineProperty(target, accessor, {
                get() { state.gets++; return 4; }, configurable: true
            });
            console.log(Reflect.deleteProperty(target, accessor), state.gets,
                Object.getOwnPropertyDescriptor(target, accessor) === undefined);
            const sealed: any = {}, frozen: any = {};
            Object.defineProperty(sealed, key, { value: 5, configurable: true });
            Object.defineProperty(frozen, key, { value: 6, configurable: true });
            Object.seal(sealed);
            Object.freeze(frozen);
            console.log(Reflect.deleteProperty(sealed, key), sealed[key],
                Reflect.deleteProperty(frozen, key), frozen[key]);
            console.log(Reflect.deleteProperty(sealed, Symbol("missing")),
                Reflect.deleteProperty(frozen, Symbol("missing")));
            """, "false 3 true\ntrue 0 true\nfalse 5 false 6\ntrue true\n"];
        yield return ["coercion-once", """
            const key = Symbol("coerced"), target: any = {}, propertyKey: any = {};
            const state = { gets: 0, calls: 0, order: "", correct: true };
            Object.defineProperty(target, key, { value: 3, configurable: true });
            target["Symbol(coerced)"] = 9;
            Object.defineProperty(propertyKey, Symbol.toPrimitive, {
                get() {
                    state.gets++; state.order += "G"; state.correct = state.correct && this === propertyKey;
                    return function(this: any, hint: string) {
                        state.calls++; state.order += "C";
                        state.correct = state.correct && this === propertyKey && hint === "string";
                        return key;
                    };
                }
            });
            function receiver() { state.order += "T"; return target; }
            function argument() { state.order += "K"; return propertyKey; }
            console.log(Reflect.deleteProperty(receiver(), argument()), state.order,
                state.gets, state.calls, state.correct, target[key] === undefined, target["Symbol(coerced)"]);
            Object.defineProperty(target, key, { value: 4, configurable: true });
            const remove: any = Reflect.deleteProperty;
            console.log(remove(target, propertyKey), state.order, state.gets, state.calls,
                state.correct, target[key] === undefined);
            """, "true TKGC 1 1 true true 9\ntrue TKGCGC 2 2 true true\n"];
        yield return ["coercion-errors", """
            const key = Symbol("retained"), target: any = {}, marker = {};
            Object.defineProperty(target, key, { value: 3, configurable: true });
            const state = { calls: 0, hint: "" };
            const throwing: any = {
                [Symbol.toPrimitive](hint: string) { state.calls++; state.hint = hint; throw marker; }
            };
            try { Reflect.deleteProperty(target, throwing); }
            catch (error) { console.log(error === marker, state.calls, state.hint, target[key]); }
            const noncallable: any = { [Symbol.toPrimitive]: 1 };
            const objectResult: any = { [Symbol.toPrimitive]() { return {}; } };
            for (const invalid of [noncallable, objectResult]) {
                try { Reflect.deleteProperty(target, invalid); }
                catch (error) { console.log(error instanceof TypeError, target[key]); }
            }
            """, "true 1 string 3\ntrue 3\ntrue 3\n"];
        yield return ["target-before-key", """
            const state = { calls: 0 }, propertyKey: any = {
                [Symbol.toPrimitive]() { state.calls++; return Symbol("unused"); }
            };
            for (const target of [null, undefined, 1]) {
                try { Reflect.deleteProperty(target as any, propertyKey); }
                catch (error) { console.log(error instanceof TypeError); }
            }
            console.log(state.calls);
            """, "true\ntrue\ntrue\n0\n"];
        yield return ["string-bound-controls", """
            function add(first: number, second: number) { return first + second; }
            const bound: any = add.bind(null, 1), remove: any = Reflect.deleteProperty;
            console.log(remove(bound, "name"), Object.getOwnPropertyDescriptor(bound, "name") === undefined,
                remove(bound, "length"), Object.getOwnPropertyDescriptor(bound, "length") === undefined,
                bound(2));
            const target: any = { plain: 3 };
            console.log(remove(target, "plain"), Object.hasOwn(target, "plain"), remove(target, "missing"));
            """, "true true true true 3\ntrue false true\n"];
    }

    public static IEnumerable<object[]> ProxyCases()
    {
        yield return ["proxy-forwarding", """
            const key = Symbol("same"), other = Symbol("same"), target: any = {};
            Object.defineProperty(target, key, { value: 3, configurable: true });
            Object.defineProperty(target, other, { value: 8, configurable: true });
            target["Symbol(same)"] = 9;
            const proxy: any = new Proxy(new Proxy(target, {}), {}), remove: any = Reflect.deleteProperty;
            console.log(remove(proxy, key), target[key] === undefined, target[other],
                target["Symbol(same)"], Object.getOwnPropertySymbols(target).length);
            console.log(Reflect.deleteProperty(proxy, key), Reflect.deleteProperty(proxy, Symbol("missing")),
                target[other]);
            """, "true true 8 9 1\ntrue true 8\n"];
        yield return ["proxy-trap-identity", """
            const key = Symbol("key"), other = Symbol("key"), target: any = {};
            const state = { conversions: 0, calls: 0, hint: "", correct: true };
            Object.defineProperty(target, key, { value: 3, configurable: true });
            const propertyKey: any = {
                [Symbol.toPrimitive](hint: string) { state.conversions++; state.hint = hint; return key; }
            };
            const handler: any = {
                deleteProperty(this: any, actual: any, property: any) {
                    state.calls++;
                    state.correct = state.correct && this === handler && actual === target
                        && property === key && property !== other;
                    return Reflect.deleteProperty(actual, property);
                }
            };
            const proxy: any = new Proxy(target, handler);
            console.log(Reflect.deleteProperty(proxy, propertyKey), state.conversions, state.calls,
                state.hint, state.correct, Object.hasOwn(target, key));
            """, "true 1 1 string true false\n"];
        yield return ["proxy-trap-errors", """
            const key = Symbol("key"), target: any = {}, marker = {};
            const state: any = { result: false, calls: 0 };
            Object.defineProperty(target, key, { value: 3, configurable: true });
            const handler: any = { deleteProperty() { state.calls++; return state.result; } };
            const proxy: any = new Proxy(target, handler);
            for (const result of [false, 0, undefined]) {
                state.result = result;
                console.log(Reflect.deleteProperty(proxy, key), Object.hasOwn(target, key));
            }
            state.result = {};
            console.log(Reflect.deleteProperty(proxy, key), Object.hasOwn(target, key), state.calls);
            handler.deleteProperty = function() { throw marker; };
            try { Reflect.deleteProperty(proxy, key); }
            catch (error) { console.log(error === marker); }
            handler.deleteProperty = 1;
            try { Reflect.deleteProperty(proxy, key); }
            catch (error) { console.log(error instanceof TypeError); }
            const revocable: any = Proxy.revocable(target, {});
            revocable.revoke();
            try { Reflect.deleteProperty(revocable.proxy, key); }
            catch (error) { console.log(error instanceof TypeError); }
            """, "false true\nfalse true\nfalse true\ntrue true 4\ntrue\ntrue\ntrue\n"];
        yield return ["proxy-invariants", """
            const key = Symbol("fixed"), locked: any = {};
            Object.defineProperty(locked, key, { value: 3, configurable: false });
            const reportsSuccess: any = { deleteProperty() { return true; } };
            try { Reflect.deleteProperty(new Proxy(locked, reportsSuccess), key); }
            catch (error) { console.log(error instanceof TypeError, locked[key]); }
            console.log(Reflect.deleteProperty(new Proxy(locked, { deleteProperty() { return false; } }), key));
            const nonextensible: any = {};
            Object.defineProperty(nonextensible, key, { value: 4, configurable: true });
            Object.preventExtensions(nonextensible);
            try { Reflect.deleteProperty(new Proxy(nonextensible, reportsSuccess), key); }
            catch (error) { console.log(error instanceof TypeError, nonextensible[key]); }
            const absent: any = {};
            Object.preventExtensions(absent);
            console.log(Reflect.deleteProperty(new Proxy(absent, reportsSuccess), key), Object.hasOwn(absent, key));
            """, "true 3\nfalse\ntrue 4\ntrue false\n"];
    }

    public static IEnumerable<object[]> ModeCases()
    {
        foreach (var row in OrdinaryCases())
        {
            // #2358 owns interpreted ordinary Symbol deletion, #2393 key coercion,
            // and #2314 primitive targets, including the #2413 target-order collateral control.
            // Preserve the ordinary string control in both modes.
            if ((string)row[0] == "string-bound-controls")
                yield return [row[0], row[1], row[2], ExecutionMode.Interpreted];
            yield return [row[0], row[1], row[2], ExecutionMode.Compiled];
        }
    }

    [Theory, MemberData(nameof(ModeCases))]
    public void ReflectDeletionPreservesPropertyKeysAndDescriptors(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, MemberData(nameof(ProxyCases))]
    public void CompiledProxyDeletionPreservesSymbolForwardingAndInvariants(
        string name, string source, string expected)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.RunCompiled(source));
    }
}
