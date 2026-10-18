using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class BuiltInMethodOwnPropertyTests
{
    [Theory, ModeData]
    public void NativeMethodValuesHaveOrdinaryOwnDescriptors(ExecutionMode mode)
    {
        const string source = """
            const method: any = Math.abs, key = Symbol("marker");
            Object.defineProperty(method, "marker", { value: 7, writable: true, enumerable: true, configurable: true });
            console.log(method.marker, method === Math.abs, method(-3), Object.keys(method).join(","));
            console.log(Reflect.defineProperty(method, key, { value: 8, writable: true, configurable: true }), method[key]);
            console.log(Reflect.set(method, "marker", 9), Reflect.get(method, "marker"),
                Reflect.getOwnPropertyDescriptor(method, key).value);
            Object.defineProperty(method, "marker", { writable: false });
            const descriptor: any = Object.getOwnPropertyDescriptor(method, "marker");
            console.log(descriptor.value, descriptor.writable, descriptor.enumerable, descriptor.configurable);
            console.log(Reflect.deleteProperty(method, key), Object.hasOwn(method, key));
            console.log(delete method.marker, Object.hasOwn(method, "marker"), method(-4));
            """;
        Assert.Equal("7 true 3 marker\ntrue 8\ntrue 9 8\n9 false true true\ntrue false\ntrue false 4\n",
            TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void NativeMethodAccessorsAndIntegrityUseTheSameStorage(ExecutionMode mode)
    {
        const string source = """
            const method: any = Math.floor, state = { gets: 0, value: 0 }, receiver = { value: 12 };
            Object.defineProperty(method, "value", {
                get: function(this: any) { state.gets++; return this === method ? state.value : this.value; },
                set: function(this: any, value: any) { state.value = value; }, enumerable: true, configurable: true
            });
            console.log(Reflect.get(method, "value", receiver), Reflect.set(method, "value", 7), method.value, state.gets);
            Object.freeze(method);
            const descriptor: any = Object.getOwnPropertyDescriptor(method, "value");
            console.log(descriptor.configurable, Object.isFrozen(method), Reflect.set(method, "value", 8), method.value);
            console.log(Reflect.defineProperty(method, "extra", { value: 1 }), Reflect.deleteProperty(method, "value"));
            console.log(method(3.8));
            """;
        Assert.Equal("12 true 7 2\nfalse true true 8\nfalse false\n3\n", TestHarness.Run(source, mode));
    }

    [Fact]
    public void NativeMethodMutationsRemainInTheirInterpreterRealm()
    {
        Assert.Equal("7\n", TestHarness.RunInterpreted("""
            const method: any = Math.abs;
            Object.defineProperty(method, "marker", { value: 7 });
            Object.defineProperty(method, "name", { value: "changed" });
            console.log(method.marker);
            """));
        Assert.Equal("true abs 1\n", TestHarness.RunInterpreted("""
            const method: any = Math.abs;
            console.log(method.marker === undefined, method.name, method.length);
            """));
    }

    [Theory, ModeData]
    public void NativeMethodOperatorsAndProxyProtocolsShareDescriptors(ExecutionMode mode)
    {
        const string source = """
            const method: any = Math.max, key = Symbol("count"), state = { conversions: 0 };
            method.count = 1; method[key] = 2;
            // #2367: keep the checker from rejecting a nonnumeric compound key.
            function symbolKey(): any { return key; }
            ++method.count; method[symbolKey()] += 3;
            method.extra ??= 6;
            function updateKey(): any { state.conversions++; return key; }
            console.log(method[updateKey()]++, method[key], method.count, method.extra, state.conversions);
            const proxy: any = new Proxy(method, {});
            console.log("count" in proxy, Reflect.get(proxy, "count"), Object.keys(proxy).join(","));
            console.log(Reflect.deleteProperty(proxy, "count"), Object.hasOwn(method, "count"));
            Object.defineProperty(method, "locked", { value: 7 });
            try { Reflect.get(new Proxy(method, { get() { return 8; } }), "locked"); }
            catch (error) { console.log(error instanceof TypeError); }
            Object.preventExtensions(method);
            try { Reflect.ownKeys(new Proxy(method, { ownKeys() { return []; } })); }
            catch (error) { console.log(error instanceof TypeError); }
            console.log(method(3, 4));
            """;
        Assert.Equal("5 6 2 6 1\ntrue 2 count,extra\ntrue false\ntrue\ntrue\n4\n", TestHarness.Run(source, mode));
    }

    [Fact]
    public void NativeMethodUpdateCoercesItsObjectKeyOnce()
    {
        // #2595: compiled GetIndex rejects an object key producing a Symbol.
        const string source = """
            const method: any = Math.max, key = Symbol("count"), state = { calls: 0 };
            method[key] = 5;
            const propertyKey: any = { [Symbol.toPrimitive](hint: string) {
                state.calls++; console.log(hint); return key;
            } };
            console.log(method[propertyKey]++, method[key], state.calls);
            """;
        Assert.Equal("string\n5 6 1\n", TestHarness.Run(source, ExecutionMode.Interpreted));
    }

    [Fact]
    public void TransferredNativeOwnersAndMethodsKeepTheirOwnProperties()
    {
        using var first = new SharpTS.Execution.Interpreter();
        using var second = new SharpTS.Execution.Interpreter();
        var math = first.GetMath();
        var method = first.GetProperty(math, "abs");
        Assert.Same(method, second.GetProperty(math, "abs"));
        Assert.Same(method, second.LocalizeBuiltInValue(method));
        Assert.NotSame(method, second.GetProperty(second.GetMath(), "abs"));
    }

    [Theory, ModeData]
    public void FixedGeneratorPrototypeMethodsKeepTheirStoredIdentity(ExecutionMode mode)
    {
        const string source = """
            function* values() { yield 1; }
            const prototype: any = Object.getPrototypeOf(Object.getPrototypeOf(values()));
            const iteratorPrototype: any = Object.getPrototypeOf(prototype);
            const next = prototype.next, iterator = iteratorPrototype[Symbol.iterator];
            Object.freeze(prototype); Object.freeze(iteratorPrototype);
            const handler = { get(target: any, key: any, receiver: any) { return Reflect.get(target, key, receiver); } };
            console.log(Reflect.get(new Proxy(prototype, handler), "next") === next,
                Reflect.get(new Proxy(iteratorPrototype, handler), Symbol.iterator) === iterator);
            """;
        Assert.Equal("true true\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void NativeApiChildrenAndNamespaceAliasesKeepIdentity(ExecutionMode mode)
    {
        const string source = """
            const owner: any = process, method: any = process.hrtime;
            console.log(method === owner.hrtime, method.bigint === owner.hrtime.bigint);
            Object.defineProperty(method, "marker", { value: 7, configurable: true });
            console.log(owner.hrtime.marker, typeof method.bigint());
            const child = method.bigint;
            console.log(delete method.bigint, method.bigint === undefined, owner.hrtime.bigint === undefined);
            console.log(typeof child());
            """;
        Assert.Equal("true true\n7 bigint\ntrue true true\nbigint\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void NativeDescriptorValuesMatchOrdinaryAndReflectReads(ExecutionMode mode)
    {
        const string source = """
            const members: any[] = [[Math, "abs"], [JSON, "parse"], [Object, "keys"], [Array, "isArray"]];
            for (const pair of members) {
                const owner: any = pair[0], name: any = pair[1], method = owner[name];
                console.log(method === Reflect.get(owner, name),
                    method === Object.getOwnPropertyDescriptor(owner, name).value);
            }
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat("true true\n", 4)), TestHarness.Run(source, mode));
    }
}
