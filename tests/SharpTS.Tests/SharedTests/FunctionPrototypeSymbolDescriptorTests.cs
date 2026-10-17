using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class FunctionPrototypeSymbolDescriptorTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", """
            const key = Symbol("readOnly");
            Object.defineProperty(Function.prototype, key, { value: 1, writable: false, configurable: true });
            console.log(Object.getOwnPropertyDescriptor(Function.prototype, key) !== undefined,
                Object.getOwnPropertySymbols(Function.prototype).includes(key),
                Object.hasOwn(Function.prototype, key.toString()));
            """, "true true false\n"];
        yield return ["descriptors", """
            const key = Symbol("label"), other = Symbol("label"), prototype: any = Function.prototype;
            Object.defineProperty(prototype, key, { value: 1, writable: true, configurable: true });
            Object.defineProperty(prototype, other, { value: 2, writable: false, enumerable: true });
            prototype[key.toString()] = 3;
            Object.defineProperty(prototype, key, { enumerable: true });
            const descriptor = Object.getOwnPropertyDescriptor(prototype, key)!;
            console.log(prototype[key], prototype[other], prototype[key.toString()]);
            console.log(descriptor.value, descriptor.writable, descriptor.enumerable, descriptor.configurable);
            console.log(Object.getOwnPropertySymbols(prototype).includes(key),
                Object.getOwnPropertySymbols(prototype).includes(other),
                Reflect.ownKeys(prototype).includes(key), Reflect.ownKeys(prototype).includes(other));
            const descriptors: any = Object.getOwnPropertyDescriptors(prototype);
            console.log(descriptors[key].value, descriptors[other].value,
                Object.prototype.propertyIsEnumerable.call(prototype, key),
                Object.prototype.propertyIsEnumerable.call(prototype, other));
            console.log(Reflect.defineProperty(prototype, other, { value: 4 }), delete prototype[other]);
            try { Object.defineProperty(prototype, other, { value: 4 }); }
            catch (error) { console.log(error instanceof TypeError); }
            console.log(delete prototype[key], Object.hasOwn(prototype, key),
                Object.getOwnPropertyDescriptor(prototype, key) === undefined,
                Object.getOwnPropertySymbols(prototype).includes(key));
            function removeStrict() { "use strict"; return delete prototype[other]; }
            try { removeStrict(); }
            catch (error) { console.log(error instanceof TypeError); }
            """, "1 2 3\n1 true true true\ntrue true true true\n1 2 true true\nfalse false\ntrue\ntrue false true false\ntrue\n"];
        yield return ["accessors", """
            const key = Symbol("accessor"), prototype: any = Function.prototype;
            let reads = 0, writes = 0;
            function get(this: any) { reads++; return this.slot; }
            function set(this: any, value: any) { writes++; this.slot = value; }
            Object.defineProperty(prototype, key, { get, set, configurable: true });
            const descriptor = Object.getOwnPropertyDescriptor(prototype, key)!;
            console.log(descriptor.get === get, descriptor.set === set,
                Object.hasOwn(descriptor, "value"), descriptor.enumerable, descriptor.configurable);
            prototype.slot = 7;
            console.log(prototype[key], reads);
            prototype[key] = 8;
            console.log(prototype.slot, writes,
                Object.getOwnPropertyDescriptor(prototype, key)!.set === set);
            const receiver: any = { slot: 9 };
            console.log(Reflect.get(prototype, key, receiver));
            console.log(Reflect.set(prototype, key, 10, receiver), receiver.slot, prototype.slot);
            console.log(Reflect.getOwnPropertyDescriptor(prototype, key)!.get === get, reads, writes);
            console.log(delete prototype[key], Object.hasOwn(prototype, key));
            """, "true true false false true\n7 1\n8 1 true\n9\ntrue 10 8\ntrue 2 2\ntrue false\n"];
        yield return ["inherited-readonly", """
            const key = Symbol("readonly"), prototype: any = Function.prototype, constructor: any = Number;
            Object.defineProperty(prototype, key, { value: 7, writable: false, configurable: true });
            console.log(constructor[key], Object.hasOwn(constructor, key));
            console.log(Reflect.set(constructor, key, 8), constructor[key], Object.hasOwn(constructor, key));
            constructor[key] = 9;
            console.log(constructor[key], Object.hasOwn(constructor, key));
            function assignStrict() { "use strict"; constructor[key] = 10; }
            try { assignStrict(); }
            catch (error) { console.log(error instanceof TypeError); }
            function fn() {}
            const callable: any = fn;
            console.log(callable[key], Reflect.set(callable, key, 11), Object.hasOwn(callable, key));
            callable[key] = 12;
            console.log(callable[key], Object.hasOwn(callable, key));
            function assignFunctionStrict() { "use strict"; callable[key] = 13; }
            try { assignFunctionStrict(); }
            catch (error) { console.log(error instanceof TypeError); }
            console.log(Reflect.deleteProperty(prototype, key), Object.hasOwn(prototype, key));
            """, "7 false\nfalse 7 false\n7 false\ntrue\n7 false false\n7 false\ntrue\ntrue false\n"];
        const string generatorSource = """
            const key = Symbol("shadow"), prototype: any = Function.prototype;
            Object.defineProperty(prototype, key, { value: 1, writable: false, configurable: true });
            function* values() { yield 1; }
            const fn: any = values, immediate: any = Object.getPrototypeOf(fn);
            Object.defineProperty(immediate, key, { value: 2, writable: true, configurable: true });
            console.log(fn[key], Object.hasOwn(fn, key));
            fn[key] = 3;
            console.log(fn[key], Object.hasOwn(fn, key));
            const other: any = function*() { yield 1; };
            console.log(other[key], Reflect.set(other, key, 4), other[key]);
            """;
        yield return ["generator-shadow", generatorSource, "2 false\n3 true\n2 true 4\n"];
        yield return ["async-generator-shadow", generatorSource.Replace("function*", "async function*"),
            "2 false\n3 true\n2 true 4\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void FunctionPrototypeSymbolsPreserveDescriptors(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Fact]
    public void InterpretedFunctionPrototypeSymbolDefinitionsStayWithinTheirRealm()
    {
        const string first = """
            Object.defineProperty(Function.prototype, Symbol.iterator, { value: 7, configurable: true });
            console.log(Object.hasOwn(Function.prototype, Symbol.iterator));
            """;
        const string second = """
            console.log(Object.hasOwn(Function.prototype, Symbol.iterator));
            """;
        Assert.Equal("true\n", TestHarness.RunInterpreted(first));
        Assert.Equal("false\n", TestHarness.RunInterpreted(second));
    }
}
