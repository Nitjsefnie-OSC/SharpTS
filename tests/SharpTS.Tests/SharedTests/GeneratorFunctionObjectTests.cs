using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class GeneratorFunctionObjectTests
{
    private const string Functions = """
        function* declared(value: any) { yield value; }
        async function* asyncDeclared(value: any) { yield value; }
        const expression: any = function* namedExpression(value: any) { yield value; };
        const asyncExpression: any = async function* namedAsyncExpression(value: any) { yield value; };
        const functions: any[] = [declared, asyncDeclared, expression, asyncExpression];
        """;

    private const string NativeFunctions = """
        function* declared(value: any) { yield value; }
        async function* asyncDeclared(value: any) { yield value; }
        let expression: any;
        let asyncExpression: any;
        {
            const offset = 0;
            expression = function* namedExpression(value: any) { yield value + offset; };
            asyncExpression = async function* namedAsyncExpression(value: any) { yield value + offset; };
        }
        const functions: any[] = [declared, asyncDeclared, expression, asyncExpression];
        """;

    public static string AssignmentSource => Functions + """
        for (const f of functions) {
            f.tag = "retained";
            f["other"] = 43;
            console.log(typeof f, f.tag, f["other"], f.length);
            const length: any = Object.getOwnPropertyDescriptor(f, "length");
            const name: any = Object.getOwnPropertyDescriptor(f, "name");
            console.log(length.writable, length.enumerable, length.configurable,
                name.value === f.name, name.writable, name.enumerable, name.configurable);
        }
        console.log(declared.name, asyncDeclared.name, expression.name, asyncExpression.name);
        console.log(declared(42).next().value, expression(44).next().value);
        async function run() {
            console.log((await asyncDeclared(43).next()).value, (await asyncExpression(45).next()).value);
        }
        run();
        """;

    public static string AssignmentExpected => string.Concat(Enumerable.Repeat(
        "function retained 43 1\nfalse false true true false false true\n", 4))
        + "declared asyncDeclared namedExpression namedAsyncExpression\n42 44\n43 45\n";

    public static string DescriptorSource => Functions + """
        const symbol = Symbol("own");
        for (const f of functions) {
            f.tag = 40;
            f.empty = undefined;
            const counter = { reads: 0 };
            Object.defineProperty(f, "read", { configurable: true, enumerable: true,
                get: function() { counter.reads++; return this === f ? this.tag : -1; },
                set: function(value: any) { this.tag = value; } });
            Object.defineProperty(f, "setterOnly", { configurable: true, set: function(value: any) { this.tag = value; } });
            Object.defineProperty(f, symbol, { configurable: true, enumerable: true,
                get: function() { return this === f ? this.tag : -2; },
                set: function(value: any) { this.tag = value; } });
            console.log("empty" in f, "setterOnly" in f, f.hasOwnProperty("empty"), counter.reads);
            console.log(f.read, f["read"], Reflect.get(f, "read"), f[symbol]);
            f["read"] = 41;
            f[symbol] = 42;
            console.log(f.read, f.setterOnly, counter.reads, f.propertyIsEnumerable(symbol));
            const own: any = Reflect.getOwnPropertyDescriptor(f, symbol);
            console.log(own.enumerable, own.configurable, Object.getOwnPropertySymbols(f)[0] === symbol);
            console.log(Object.keys(f).join(","), Object.values(f)[2], Object.getOwnPropertyNames(f).includes("setterOnly"));
            Object.assign(f, { read: 45, [symbol]: 46 });
            const assigned: any = Object.assign({}, f);
            console.log(f.tag, assigned.read, assigned[symbol]);
            console.log(delete f.read, delete f[symbol], Reflect.deleteProperty(f, "setterOnly"), "read" in f);
        }
        """;

    public static string DescriptorExpected => string.Concat(Enumerable.Repeat(
        "true true true 0\n40 40 40 40\n42 undefined 4 true\ntrue true true\ntag,empty,read 42 true\n46 46 46\ntrue true true false\n", 4));

    public static string IntegritySource => "\"use strict\";\n" + Functions + """
        for (const f of functions) {
            f.tag = 42;
            Object.freeze(f);
            console.log(Object.isFrozen(f), Object.isSealed(f), Object.isExtensible(f), Reflect.isExtensible(f));
            try { f.tag = 43; } catch (e) { console.log(e instanceof TypeError); }
            try { f["new"] = 44; } catch (e) { console.log(e instanceof TypeError); }
            console.log(f.tag, Reflect.defineProperty(f, "new", { value: 44 }), Reflect.deleteProperty(f, "tag"));
        }
        function* sealed() { yield 1; }
        async function* prevented() { yield 1; }
        const s: any = sealed, p: any = prevented;
        s.tag = 1;
        Object.seal(s);
        s.tag = 2;
        console.log(Object.isSealed(s), Object.isFrozen(s), s.tag, Reflect.deleteProperty(s, "tag"));
        console.log(Reflect.preventExtensions(p), Object.isExtensible(p), Reflect.set(p, "new", 3));
        """;

    public static string IntegrityExpected => string.Concat(Enumerable.Repeat(
        "true true false false\ntrue\ntrue\n42 false false\n", 4))
        + "true false 2 false\ntrue false false\n";

    public static string PartialDescriptorSource => Functions + """
        const key = Symbol("partial");
        for (const f of functions) {
            f.tag = 41;
            Object.defineProperty(f, "tag", { writable: false });
            let descriptor: any = Object.getOwnPropertyDescriptor(f, "tag");
            console.log(descriptor.value, descriptor.writable, descriptor.enumerable, descriptor.configurable);
            console.log(Reflect.defineProperty(f, "tag", { value: 42 }));
            descriptor = Object.getOwnPropertyDescriptor(f, "tag");
            console.log(descriptor.value, descriptor.writable, descriptor.enumerable, descriptor.configurable);
            f[key] = 43;
            console.log(Object.defineProperty(f, key, { writable: false }) === f);
            descriptor = Object.getOwnPropertyDescriptor(f, key);
            console.log(descriptor.value, descriptor.writable, descriptor.enumerable, descriptor.configurable);
            Object.defineProperty(f, key, { value: 44 });
            descriptor = Object.getOwnPropertyDescriptor(f, key);
            console.log(descriptor.value, descriptor.writable, descriptor.enumerable, descriptor.configurable);
            const getter: any = function() { return 45; };
            const setter: any = function(value: any) { this.tag = value; };
            Object.defineProperty(f, "accessor", { get: getter, set: setter, enumerable: true, configurable: true });
            console.log(Reflect.defineProperty(f, "accessor", { enumerable: false }));
            descriptor = Object.getOwnPropertyDescriptor(f, "accessor");
            console.log(descriptor.get === getter, descriptor.set === setter, descriptor.enumerable, descriptor.configurable);
            Object.defineProperty(f, "accessor", { get: undefined });
            descriptor = Reflect.getOwnPropertyDescriptor(f, "accessor");
            console.log(descriptor.get === undefined, descriptor.set === setter, descriptor.enumerable, descriptor.configurable);
        }
        """;

    public static string PartialDescriptorExpected => string.Concat(Enumerable.Repeat(
        "41 false true true\ntrue\n42 false true true\ntrue\n43 false true true\n44 false true true\ntrue\ntrue true false true\ntrue true false true\n", 4));

    [Theory, ModeData]
    public void AllGeneratorFunctionFormsRetainOwnPropertiesAndMetadata(ExecutionMode mode)
        => Assert.Equal(AssignmentExpected, TestHarness.Run(AssignmentSource, mode));

    [Theory, ModeData]
    public void DescriptorsUseTheFunctionReceiverAndPreserveOwnPresence(ExecutionMode mode)
        => Assert.Equal(DescriptorExpected, TestHarness.Run(DescriptorSource, mode));

    [Theory, ModeData]
    public void FrozenGeneratorFunctionsRejectWritesAndReflectIntegrity(ExecutionMode mode)
        => Assert.Equal(IntegrityExpected, TestHarness.Run(IntegritySource, mode));

    [Theory, ModeData]
    public void PartialDescriptorsRetainAttributesValuesAndAccessorHalves(ExecutionMode mode)
        => Assert.Equal(PartialDescriptorExpected, TestHarness.Run(PartialDescriptorSource, mode));

    [Theory]
    [InlineData("assignment")]
    [InlineData("descriptors")]
    [InlineData("integrity")]
    [InlineData("partial")]
    public void NativeBlockCapturingExpressionsUseTheSameOwnPropertyProtocol(string scenario)
    {
        var (source, expected) = scenario switch
        {
            "assignment" => (AssignmentSource, AssignmentExpected),
            "descriptors" => (DescriptorSource, DescriptorExpected),
            "partial" => (PartialDescriptorSource, PartialDescriptorExpected),
            _ => (IntegritySource, IntegrityExpected),
        };
        // These expressions survive generator lifting because they capture a
        // block binding. Exercise the native expression wrappers in interpretation.
        Assert.Equal(expected, TestHarness.RunInterpreted(source.Replace(Functions, NativeFunctions)));
    }

    [Fact]
    public void InterpreterAdaptersShareDescriptorsAndDoNotRestoreDeletedMetadata()
    {
        const string source = """
            const object: any = { *method() { yield this.tag; }, async *asyncMethod() { yield this.tag; } };
            class C { *method() { yield this.tag; } async *asyncMethod() { yield this.tag; } tag: any = 9; }
            const instance: any = new C();
            for (const f of [object.method, object.asyncMethod, instance.method, instance.asyncMethod]) {
                f.tag = 42;
                delete f.name;
                delete f.length;
                const copied: any = { f };
                console.log(copied.f.tag, copied.f.hasOwnProperty("name"), copied.f.hasOwnProperty("length"));
                console.log(copied.f.name === undefined, copied.f.length === undefined);
            }
            console.log(object.method.tag, object.asyncMethod.tag, instance.method.tag, instance.asyncMethod.tag);
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat("42 false false\ntrue true\n", 4)) + "42 42 42 42\n",
            TestHarness.RunInterpreted(source));
    }

    [Fact]
    public void InterpreterProxyIntegrityChecksUseGeneratorOwnPropertyStorage()
    {
        const string source = """
            function* frozen() { yield 1; }
            async function* prevented() { yield 1; }
            const first: any = frozen, second: any = prevented;
            Object.freeze(first);
            Object.preventExtensions(second);
            for (const f of [first, second]) {
                const transparent: any = new Proxy(f, {});
                console.log(Reflect.isExtensible(transparent));
                const extensibility: any = new Proxy(f, { isExtensible: function() { return true; } });
                try { Reflect.isExtensible(extensibility); } catch (e) { console.log(e instanceof TypeError); }
                const defining: any = new Proxy(f, { defineProperty: function() { return true; } });
                try { Reflect.defineProperty(defining, "new", { value: 42, configurable: true }); }
                catch (e) { console.log(e instanceof TypeError); }
            }
            """;
        Assert.Equal("false\ntrue\ntrue\nfalse\ntrue\ntrue\n", TestHarness.RunInterpreted(source));
    }

    [Theory, ModeData]
    public void ExplicitOwnPrototypeParticipatesInInstanceof(ExecutionMode mode)
    {
        const string source = """
            function* generator() { yield 42; }
            const f: any = generator;
            const prototype = {};
            f.prototype = prototype;
            console.log(Object.create(prototype) instanceof f, {} instanceof f, 1 instanceof f);
            f.prototype = null;
            try { console.log({} instanceof f); } catch (e) { console.log(e instanceof TypeError); }
            f.temporary = 42;
            console.log(delete f.temporary, f.hasOwnProperty("temporary"));
            """;
        Assert.Equal("true false false\ntrue\ntrue false\n", TestHarness.Run(source, mode));
    }
}
