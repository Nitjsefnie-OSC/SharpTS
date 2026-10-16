using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class DeletedNumberMetadataDescriptorTests
{
    public const string IssueSource = """
        const constructor: any = Number;
        console.log(delete constructor.name, Object.hasOwn(constructor, "name"),
            Object.getOwnPropertyDescriptor(constructor, "name") === undefined);
        """;

    public const string LifecycleSource = """
        const constructor: any = Number;
        const original = Object.getOwnPropertyDescriptor(constructor, "name")!;
        console.log(original.value, original.writable, original.enumerable, original.configurable);
        Object.defineProperty(constructor, "name", { value: "Numeric", configurable: true });
        console.log(delete constructor["name"], Object.hasOwn(constructor, "name"), Object.getOwnPropertyDescriptor(constructor, "name") === undefined);
        Object.defineProperty(constructor, "name", { value: "Reborn", writable: true, enumerable: true, configurable: true });
        const restored = Object.getOwnPropertyDescriptor(constructor, "name")!;
        console.log(restored.value, restored.writable, restored.enumerable, restored.configurable, Object.hasOwn(constructor, "name"));
        console.log(Reflect.deleteProperty(constructor, "name"), Object.hasOwn(constructor, "name"), Object.getOwnPropertyDescriptor(constructor, "name") === undefined);
        console.log(Reflect.deleteProperty(constructor, "length"), Object.hasOwn(constructor, "length"), Object.getOwnPropertyDescriptor(constructor, "length") === undefined);
        Object.defineProperty(constructor, "name", { value: "Locked", configurable: false });
        console.log(Reflect.deleteProperty(constructor, "name"), Object.getOwnPropertyDescriptor(constructor, "name")!.value, Object.getOwnPropertyDescriptor(constructor, "name")!.configurable);
        """;

    public const string RestorationSource = """
        const constructor: any = Number, read: any = Object.getOwnPropertyDescriptor;
        const define: any = Object.defineProperty, state = { reads: 0 };
        function getter() { state.reads++; return "restored"; }
        for (const key of ["name", "length"]) {
            console.log(delete constructor[key], read(constructor, key) === undefined);
            define(constructor, key, { value: undefined, writable: true, configurable: true });
            const missing = read(constructor, key);
            console.log(Object.hasOwn(missing, "value"), missing.value === undefined, missing.writable,
                !missing.enumerable, missing.configurable, Object.hasOwn(constructor, key));
            console.log(Reflect.deleteProperty(constructor, key), read(constructor, key) === undefined);
            define(constructor, key, { value: null, enumerable: true, configurable: true });
            const nil = read(constructor, key);
            console.log(Object.hasOwn(nil, "value"), nil.value === null, nil.writable, nil.enumerable, nil.configurable);
            console.log(delete constructor[key], read(constructor, key) === undefined);
            define(constructor, key, { get: getter, configurable: true });
            const accessor = read(constructor, key);
            console.log(accessor.get === getter, accessor.set === undefined,
                !Object.hasOwn(accessor, "value"), !Object.hasOwn(accessor, "writable"), state.reads);
            console.log(Reflect.deleteProperty(constructor, key), read(constructor, key) === undefined, state.reads);
        }
        """;

    public const string InheritedGetterSource = """
        const constructor: any = Number, prototype: any = Object.getPrototypeOf(constructor);
        const originalName = Object.getOwnPropertyDescriptor(prototype, "name")!;
        const originalLength = Object.getOwnPropertyDescriptor(prototype, "length")!;
        const originalPrototype = Object.getOwnPropertyDescriptor(constructor, "prototype")!;
        const state = { reads: 0 }, marker = {};
        function getter() { state.reads++; throw marker; }
        try {
            for (const key of ["name", "length"]) {
                Reflect.deleteProperty(constructor, key);
                Object.defineProperty(prototype, key, { get: getter, configurable: true });
                console.log(Object.getOwnPropertyDescriptor(constructor, key) === undefined,
                    Object.hasOwn(constructor, key), state.reads);
            }
            const descriptor = Object.getOwnPropertyDescriptor(constructor, "prototype")!;
            console.log(Reflect.deleteProperty(constructor, "prototype"),
                descriptor.value === originalPrototype.value, descriptor.configurable, state.reads);
        } finally {
            Object.defineProperty(prototype, "name", originalName);
            Object.defineProperty(prototype, "length", originalLength);
        }
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "true false true\n"];
        yield return ["lifecycle", LifecycleSource,
            "Number false false true\ntrue false true\nReborn true true true true\n"
            + "true false true\ntrue false true\nfalse Locked false\n"];
        yield return ["restoration", RestorationSource,
            "true true\ntrue true true true true true\ntrue true\ntrue true false true true\n"
            + "true true\ntrue true true true 0\ntrue true 0\n"
            + "true true\ntrue true true true true true\ntrue true\ntrue true false true true\n"
            + "true true\ntrue true true true 0\ntrue true 0\n"];
        yield return ["inherited-getter", InheritedGetterSource,
            "true false 0\ntrue false 0\nfalse true false 0\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void DeletedNumberMetadataStopsSynthesizingOwnDescriptors(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
