using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class FunctionPrototypeMetadataTests
{
    public const string OriginalSource = """
        console.log(Function.prototype.name === "", Function.prototype.length === 0);
        """;

    public const string KeyOrderSource = """
        const prototype: any = Function.prototype, originalCall: any = prototype.call;
        const selected = ["length", "name", "constructor", "call", "extra", "hidden", "2", "10"];
        function keys() { return Object.getOwnPropertyNames(prototype).filter(key => selected.includes(key)).join(","); }
        console.log(keys());
        let reads = 0;
        prototype.extra = 1;
        Object.defineProperty(prototype, "hidden", { get() { reads++; return 2; }, configurable: true });
        prototype[10] = 10; prototype[2] = 2;
        console.log(keys(), reads);
        Object.defineProperty(prototype, "call", {
            value: originalCall, writable: true, enumerable: false, configurable: true
        });
        console.log(keys(), reads);
        const removed = delete prototype.call, withoutCall = keys();
        Object.defineProperty(prototype, "call", {
            value: originalCall, writable: true, enumerable: false, configurable: true
        });
        console.log(removed, withoutCall);
        console.log(keys(), reads);
        delete prototype.name;
        Object.defineProperty(prototype, "name", { value: "changed", configurable: true });
        console.log(keys(), reads);
        Object.defineProperty(prototype, "visible", {
            get: function(this: any) { reads++; return this === prototype ? 3 : 9; },
            enumerable: true, configurable: true
        });
        console.log(Object.keys(prototype).join(","), reads);
        console.log(Object.values(prototype).join(","), reads);
        console.log(Object.entries(prototype).map(entry => entry.join(":")).join(","), reads);
        """;

    [Fact]
    public void InterpreterProjectionPreservesIntrinsicAndRecreatedKeyOrder()
        => Assert.Equal("length,name,constructor,call\n"
            + "2,10,length,name,constructor,call,extra,hidden 0\n"
            + "2,10,length,name,constructor,call,extra,hidden 0\n"
            + "true 2,10,length,name,constructor,extra,hidden\n"
            + "2,10,length,name,constructor,extra,hidden,call 0\n"
            + "2,10,length,constructor,extra,hidden,call,name 0\n"
            + "2,10,extra,visible 0\n2,10,1,3 1\n2:2,10:10,extra:1,visible:3 2\n",
            TestHarness.RunInterpreted(KeyOrderSource));

    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", OriginalSource, "true true\n"];

        yield return ["descriptors-and-inherited-reads", """
            const prototype: any = Function.prototype;
            const nameDescriptor: any = Object.getOwnPropertyDescriptor(prototype, "name");
            const lengthDescriptor: any = Object.getOwnPropertyDescriptor(prototype, "length");
            console.log(Object.hasOwn(prototype, "name"), Object.hasOwn(prototype, "length"),
                nameDescriptor.value === "", lengthDescriptor.value === 0);
            console.log(nameDescriptor.writable, nameDescriptor.enumerable, nameDescriptor.configurable,
                lengthDescriptor.writable, lengthDescriptor.enumerable, lengthDescriptor.configurable);
            console.log(Object.hasOwn(nameDescriptor, "get"), Object.hasOwn(nameDescriptor, "set"),
                Object.hasOwn(lengthDescriptor, "get"), Object.hasOwn(lengthDescriptor, "set"));
            console.log(prototype.name === prototype["name"], prototype.length === prototype["length"],
                Reflect.get(prototype, "name") === "", Reflect.get(prototype, "length") === 0);
            const child: any = Object.create(prototype);
            console.log(child.name === "", child.length === 0,
                Object.hasOwn(child, "name"), Object.hasOwn(child, "length"));
            console.log(Object.prototype.propertyIsEnumerable.call(prototype, "name"),
                Object.prototype.propertyIsEnumerable.call(prototype, "length"),
                Object.keys(prototype).includes("name"), Object.keys(prototype).includes("length"));
            """, "true true true true\nfalse false true false false true\nfalse false false false\ntrue true true true\ntrue true false false\nfalse false false false\n"];

        yield return ["reflection-projections-and-symbols", """
            const prototype: any = Function.prototype, key = Symbol("metadata");
            Object.defineProperty(prototype, key, { value: 7, configurable: true });
            const names = Object.getOwnPropertyNames(prototype), keys = Reflect.ownKeys(prototype);
            const descriptors: any = Object.getOwnPropertyDescriptors(prototype);
            console.log(names.filter(name => name === "name").length,
                names.filter(name => name === "length").length,
                keys.filter((name: any) => name === "name").length,
                keys.filter((name: any) => name === "length").length);
            console.log(keys.filter((name: any) => name === key).length,
                Object.getOwnPropertySymbols(prototype).includes(key), names.includes("Symbol(metadata)"));
            console.log(Object.hasOwn(descriptors, "name"), Object.hasOwn(descriptors, "length"),
                Object.hasOwn(descriptors, key), descriptors.name.value === "", descriptors.length.value === 0,
                descriptors[key].value);
            console.log(descriptors.name.writable, descriptors.name.enumerable, descriptors.name.configurable,
                descriptors.length.writable, descriptors.length.enumerable, descriptors.length.configurable);
            console.log(Reflect.ownKeys(descriptors).includes(key),
                Object.keys(prototype).includes("name"), Object.keys(prototype).includes("length"));
            """, "1 1 1 1\n1 true false\ntrue true true true true 7\nfalse false true false false true\ntrue false false\n"];

        yield return ["configurable-lifecycle-and-getter", """
            const prototype: any = Function.prototype, child: any = Object.create(prototype);
            const originalName: any = Object.getOwnPropertyDescriptor(prototype, "name");
            const originalLength: any = Object.getOwnPropertyDescriptor(prototype, "length");
            Object.defineProperty(prototype, "name", { value: undefined });
            Object.defineProperty(prototype, "length", { value: null });
            let descriptors: any = Object.getOwnPropertyDescriptors(prototype);
            console.log(Object.hasOwn(prototype, "name"), Object.hasOwn(prototype, "length"),
                prototype.name === undefined, prototype.length === null,
                descriptors.name.value === undefined, descriptors.length.value === null);
            console.log(Object.getOwnPropertyNames(prototype).includes("name"),
                Reflect.ownKeys(prototype).includes("length"), child.name === undefined, child.length === null);
            Object.defineProperty(prototype, "name", { value: "changed" });
            Object.defineProperty(prototype, "length", { value: 17 });
            console.log(prototype.name, prototype["length"], child.name, child.length);
            console.log(delete prototype.name, delete prototype.length);
            descriptors = Object.getOwnPropertyDescriptors(prototype);
            console.log(Object.hasOwn(prototype, "name"), Object.hasOwn(prototype, "length"),
                Object.hasOwn(descriptors, "name"), Object.hasOwn(descriptors, "length"),
                Object.getOwnPropertyNames(prototype).includes("name"), Reflect.ownKeys(prototype).includes("length"));
            console.log(prototype.name === undefined, prototype.length === undefined,
                Object.getOwnPropertyDescriptor(prototype, "name") === undefined);
            let reads = 0;
            Object.defineProperty(prototype, "name", {
                get: function(this: any) { reads++; return this === prototype ? "prototype" : "child"; },
                configurable: true
            });
            descriptors = Object.getOwnPropertyDescriptors(prototype);
            const getter: any = Object.getOwnPropertyDescriptor(prototype, "name")!.get;
            console.log(reads, Object.getOwnPropertyNames(prototype).includes("name"),
                Reflect.ownKeys(prototype).includes("name"), descriptors.name.get === getter,
                Object.hasOwn(descriptors.name, "value"));
            console.log(prototype.name, reads);
            console.log(child.name, reads);
            Object.defineProperty(prototype, "name", originalName);
            Object.defineProperty(prototype, "length", originalLength);
            console.log(prototype.name === "", prototype.length === 0,
                Object.getOwnPropertyNames(prototype).includes("name"), Reflect.ownKeys(prototype).includes("length"));
            """, "true true true true true true\ntrue true true true\nchanged 17 changed 17\ntrue true\nfalse false false false false false\ntrue true true\n0 true true true false\nprototype 1\nchild 2\ntrue true true true\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void IntrinsicMetadataIsVisibleThroughPropertiesAndReflection(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void MetadataMutationsRemainInsideTheirRealm(ExecutionMode mode)
    {
        const string mutate = """
            const prototype: any = Function.prototype;
            Object.defineProperty(prototype, "name", { value: "changed" });
            delete prototype.length;
            console.log(prototype.name, Object.hasOwn(prototype, "length"));
            """;
        Assert.Equal("changed false\n", TestHarness.Run(mutate, mode));
        Assert.Equal("true true\n", TestHarness.Run(OriginalSource, mode));
    }

    [Theory, ModeData]
    public void NumberOwnMetadataDeletionFallsBackToLivePrototypeMetadata(ExecutionMode mode)
    {
        const string source = """
            const constructor: any = Number, prototype: any = Function.prototype;
            delete constructor.name; delete constructor.length;
            console.log(constructor.name === "", constructor.length === 0,
                Object.hasOwn(constructor, "name"), Object.hasOwn(constructor, "length"));
            Object.defineProperty(prototype, "name", { value: "inherited" });
            Object.defineProperty(prototype, "length", { value: 19 });
            console.log(constructor.name, constructor.length,
                Object.hasOwn(constructor, "name"), Object.hasOwn(constructor, "length"));
            Object.defineProperty(prototype, "name", { value: null });
            Object.defineProperty(prototype, "length", { value: undefined });
            console.log(constructor.name === null, constructor.length === undefined,
                Object.hasOwn(constructor, "name"), Object.hasOwn(constructor, "length"));
            """;
        Assert.Equal("true true false false\ninherited 19 false false\ntrue true false false\n",
            TestHarness.Run(source, mode));
    }
}
