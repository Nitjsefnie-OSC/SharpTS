using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ArrayConstructorPrototypeMutationTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue-custom-null-restore", """
            const A: any = Array;
            const initial = Object.getPrototypeOf(A), custom = { marker: 7 };
            try {
                Object.setPrototypeOf(A, custom);
                console.log("custom", Object.getPrototypeOf(A) === custom, A.marker);
            } catch (error) { console.log("custom error", error instanceof TypeError); }
            try {
                Object.setPrototypeOf(A, null);
                console.log("null", Object.getPrototypeOf(A) === null, typeof A.bind);
            } catch (error) { console.log("null error", error instanceof TypeError); }
            try {
                Object.setPrototypeOf(A, initial);
                console.log("restored", Object.getPrototypeOf(A) === initial);
            } catch (error) { console.log("restore error", error instanceof TypeError); }
            """, "custom true 7\nnull true undefined\nrestored true\n"];

        yield return ["own-surface-and-callability", """
            const A: any = Array, initial = Object.getPrototypeOf(A);
            const instancePrototype = Array.prototype, aliasPrototype = A.prototype;
            const from = A.from, isArray = A.isArray;
            const custom = { marker: 7 };
            for (const selected of [custom, null, initial]) {
                Object.setPrototypeOf(A, selected);
                const descriptor: any = Object.getOwnPropertyDescriptor(A, "prototype");
                console.log(Object.getPrototypeOf(A) === selected, A.prototype === aliasPrototype,
                    A.from === from, A.isArray === isArray, descriptor.writable,
                    descriptor.enumerable, descriptor.configurable);
                const called = A(1, 2), constructed = new A(3, 4), applied = Reflect.apply(A, null, [5, 6]);
                console.log(called.join(","), constructed.join(","), applied.join(","),
                    Object.getPrototypeOf(called) === instancePrototype,
                    Object.getPrototypeOf(constructed) === instancePrototype, A.isArray(applied));
            }
            """, "true true true true false false false\n1,2 3,4 5,6 true true true\n"
                + "true true true true false false false\n1,2 3,4 5,6 true true true\n"
                + "true true true true false false false\n1,2 3,4 5,6 true true true\n"];

        yield return ["array-prototype-alias-identity", """
            const A: any = Array, instancePrototype = Array.prototype;
            Object.setPrototypeOf(A, null);
            console.log(typeof A.prototype, A.prototype === instancePrototype,
                Object.getPrototypeOf(A(1, 2)) === A.prototype,
                Object.getPrototypeOf(new A(3, 4)) === A.prototype);
            """, "object true true true\n"];

        yield return ["inherited-string-symbol-receivers", """
            const A: any = Array, symbol = Symbol("value"), method = Symbol("method");
            const other = { marker: 11 }, state = { receivers: "" };
            const read = function(this: any) { return this.marker; };
            const custom: any = { marker: 7, read };
            custom[method] = read;
            Object.defineProperty(custom, "value", {
                get: function(this: any) {
                    state.receivers += this === A ? "A" : this === other ? "O" : "?";
                    return this.marker;
                }
            });
            Object.defineProperty(custom, symbol, {
                get: function(this: any) {
                    state.receivers += this === A ? "A" : this === other ? "O" : "?";
                    return this.marker + 1;
                }
            });
            Object.setPrototypeOf(A, custom);
            console.log(A.value, A["value"], Reflect.get(A, "value"), Reflect.get(A, "value", other));
            console.log(A[symbol], Reflect.get(A, symbol), Reflect.get(A, symbol, other));
            console.log(A.read === read, A["read"] === read, Reflect.get(A, "read") === read,
                A.read(), A["read"](), Reflect.apply(Reflect.get(A, "read"), other, []));
            console.log(A[method] === read, A[method](), Reflect.apply(Reflect.get(A, method), other, []));
            console.log(state.receivers);
            """, "7 7 7 11\n8 8 12\ntrue true true 7 7 11\ntrue 7 11\nAAAOAAO\n"];

        yield return ["bound-prototype-snapshot", """
            const A: any = Array, initial = Object.getPrototypeOf(A);
            const borrowedBind: any = Function.prototype.bind, custom = { marker: 7 };
            for (const selected of [custom, null]) {
                Object.setPrototypeOf(A, selected);
                const bound: any = borrowedBind.call(A, null, "first");
                Object.setPrototypeOf(A, initial);
                console.log(Object.getPrototypeOf(bound) === selected,
                    bound("second").join(","), new bound("second").join(","));
            }
            """, "true first,second first,second\ntrue first,second first,second\n"];

        yield return ["nonextensible-same-and-different", """
            const A: any = Array, custom = { marker: 7 };
            Object.setPrototypeOf(A, custom);
            Object.preventExtensions(A);
            console.log(Object.isExtensible(A), Object.setPrototypeOf(A, custom) === A,
                Reflect.setPrototypeOf(A, custom));
            console.log(Reflect.setPrototypeOf(A, null), Object.getPrototypeOf(A) === custom);
            try { Object.setPrototypeOf(A, null); }
            catch (error) { console.log(error instanceof TypeError); }
            console.log(Object.getPrototypeOf(A) === custom, A.marker, A(1, 2).join(","));
            """, "false true true\nfalse true\ntrue\ntrue 7 1,2\n"];

        yield return ["reflection-and-accessor-presence", """
            const A: any = Array, initial = Object.getPrototypeOf(A), symbol = Symbol("present");
            const state = { gets: 0 }, custom: any = {};
            console.log(Reflect.getPrototypeOf(A) === initial);
            Object.defineProperty(custom, "watched", { get() { state.gets++; return undefined; } });
            Object.defineProperty(custom, symbol, { set(value: any) {} });
            Object.setPrototypeOf(A, custom);
            console.log(Reflect.getPrototypeOf(A) === custom, "watched" in A, symbol in A,
                Reflect.has(A, "watched"), Reflect.has(A, symbol), state.gets);
            Object.setPrototypeOf(A, null);
            console.log(Reflect.getPrototypeOf(A) === null, "watched" in A, symbol in A,
                "bind" in A, state.gets);
            Object.setPrototypeOf(A, initial);
            Object.defineProperty(Function.prototype, "watched", {
                get() { state.gets++; return undefined; }, configurable: true
            });
            try { console.log("watched" in A, Reflect.has(A, "watched"), state.gets, "bind" in A); }
            finally { delete (Function.prototype as any).watched; }
            """, "true\ntrue true true true true 0\ntrue false false false 0\ntrue true 0 true\n"];

        yield return ["cycles-and-proxy-tail-preflight", """
            const A: any = Array, initial = Object.getPrototypeOf(A);
            console.log(Reflect.setPrototypeOf(A, A), Object.getPrototypeOf(A) === initial);
            try { Object.setPrototypeOf(A, A); }
            catch (error) { console.log(error instanceof TypeError); }
            const custom = {}, tail = {};
            Object.setPrototypeOf(A, custom);
            Object.setPrototypeOf(custom, tail);
            console.log(Reflect.setPrototypeOf(tail, A), Object.getPrototypeOf(tail) === Object.prototype);
            try { Object.setPrototypeOf(tail, A); }
            catch (error) { console.log(error instanceof TypeError); }
            console.log(Object.getPrototypeOf(A) === custom, Object.getPrototypeOf(custom) === tail);
            const state = { traps: 0 };
            const proxy: any = new Proxy({}, {
                getPrototypeOf() { state.traps++; return A; }
            });
            console.log(Reflect.setPrototypeOf(A, proxy), state.traps, Object.getPrototypeOf(A) === proxy);
            console.log(Object.setPrototypeOf(A, proxy) === A, state.traps);
            Object.setPrototypeOf(A, initial);
            console.log(Object.getPrototypeOf(A) === initial, state.traps);
            """, "false true\ntrue\nfalse true\ntrue\ntrue true\ntrue 0 true\ntrue 0\ntrue 0\n"];

        yield return ["derived-constructor-cycle", """
            class Derived extends Array {}
            const A: any = Array, initial = Object.getPrototypeOf(A);
            console.log(Reflect.setPrototypeOf(A, Derived), Object.getPrototypeOf(A) === initial);
            try { Object.setPrototypeOf(A, Derived); }
            catch (error) { console.log(error instanceof TypeError); }
            console.log(Object.getPrototypeOf(A) === initial, new Derived(1, 2).join(","));
            """, "false true\ntrue\ntrue 1,2\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            // #2315: compiled native constructor aliases omit their prototype property.
            .Where(mode => mode == ExecutionMode.Interpreted || (string)row[0] != "array-prototype-alias-identity")
            // #2605: compiled Array subclasses expose a distinct constructor bridge.
            .Where(mode => mode == ExecutionMode.Interpreted || (string)row[0] != "derived-constructor-cycle")
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ArrayConstructorHonorsExplicitInternalPrototype(
        string name, string source, string expected, ExecutionMode mode)
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
