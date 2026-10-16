using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class OrdinaryFunctionMetadataDescriptorTests
{
    public const string IssueSource = """
        function original() {}
        const method: any = original;
        console.log(delete method.name, Object.hasOwn(method, "name"),
            Object.getOwnPropertyDescriptor(method, "name") === undefined);
        class C { static get value() { return 1; } }
        const accessor: any = Object.getOwnPropertyDescriptor(C, "value")!.get;
        console.log(delete accessor.name, Object.hasOwn(accessor, "name"),
            Object.getOwnPropertyDescriptor(accessor, "name") === undefined);
        """;

    public const string FormsSource = """
        function declaration(first: any, second: any) {}
        const expression = function named(first: any) {};
        const arrow = (first: any) => first;
        const object = { method(first: any) {} };
        class C {
            method(first: any) {}
            static get value() { return 1; }
            set value(next: number) {}
        }
        const getter: any = Object.getOwnPropertyDescriptor(C, "value")!.get;
        const setter: any = Object.getOwnPropertyDescriptor(C.prototype, "value")!.set;
        for (const target of [declaration, expression, arrow, object.method,
            new C().method, getter, setter] as any[]) {
            console.log(delete target.name, Object.hasOwn(target, "name"),
                Object.getOwnPropertyDescriptor(target, "name") === undefined);
            console.log(delete target.length, Object.hasOwn(target, "length"),
                Object.getOwnPropertyDescriptor(target, "length") === undefined);
            Object.defineProperty(target, "name", {
                value: "renamed", writable: true, enumerable: true, configurable: true
            });
            Object.defineProperty(target, "length", {
                value: 9, writable: true, enumerable: true, configurable: true
            });
            for (const key of ["name", "length"]) {
                const descriptor: any = Object.getOwnPropertyDescriptor(target, key);
                console.log(descriptor.value === target[key], descriptor.writable,
                    descriptor.enumerable, descriptor.configurable);
            }
        }
        """;

    public const string AccessorReplacementSource = """
        function original() {}
        const target: any = original;
        let reads = 0;
        Object.defineProperty(target, "name", {
            get() { reads++; return "own"; }, enumerable: true, configurable: true
        });
        const descriptor: any = Object.getOwnPropertyDescriptor(target, "name");
        console.log("get" in descriptor, "set" in descriptor, "value" in descriptor,
            descriptor.enumerable, descriptor.configurable, reads);
        console.log(target.name, reads);
        console.log(delete target.name, Object.hasOwn(target, "name"),
            Object.getOwnPropertyDescriptor(target, "name") === undefined);
        Object.defineProperty(target, "length", { get: undefined, configurable: true });
        const empty: any = Object.getOwnPropertyDescriptor(target, "length");
        console.log("get" in empty, empty.get === undefined, "value" in empty);
        console.log(delete target.length, Object.getOwnPropertyDescriptor(target, "length") === undefined);
        """;

    public const string LockedReplacementSource = """
        function original(first: any) {}
        const target: any = original;
        Object.defineProperty(target, "name", {
            value: "locked", writable: false, enumerable: true, configurable: false
        });
        const descriptor: any = Object.getOwnPropertyDescriptor(target, "name");
        console.log(delete target.name, Object.hasOwn(target, "name"), descriptor.value,
            descriptor.writable, descriptor.enumerable, descriptor.configurable);
        """;

    public const string InheritedGetterSource = """
        function original(first: any) {}
        const target: any = original, prototype: any = Function.prototype;
        const state = { reads: 0 };
        const originalName = Object.getOwnPropertyDescriptor(prototype, "name")!;
        const originalLength = Object.getOwnPropertyDescriptor(prototype, "length")!;
        try {
            for (const key of ["name", "length"]) {
                Object.defineProperty(prototype, key, {
                    get() { state.reads++; return "parent"; }, configurable: true
                });
                console.log(delete target[key], Object.hasOwn(target, key),
                    Object.getOwnPropertyDescriptor(target, key) === undefined, state.reads);
                console.log(Object.getOwnPropertyNames(target).includes(key),
                    Object.hasOwn(Object.getOwnPropertyDescriptors(target), key));
            }
        } finally {
            Object.defineProperty(prototype, "name", originalName);
            Object.defineProperty(prototype, "length", originalLength);
        }
        """;

    public const string ComputedMethodSource = """
        class C { [""]() {} static ["m"]() {} }
        const owner: any = C, instance: any = new C();
        for (const target of [instance[""], owner.m]) {
            console.log(delete target.name, Object.hasOwn(target, "name"), Object.getOwnPropertyDescriptor(target, "name") === undefined);
        }
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "true false true\ntrue false true\n"];
        yield return ["ordinary-callable-forms", FormsSource,
            string.Concat(Enumerable.Repeat("true false true\ntrue false true\ntrue true true true\ntrue true true true\n", 7))];
        yield return ["accessor-replacement", AccessorReplacementSource,
            "true true false true true 0\nown 1\ntrue false true\ntrue true false\ntrue true\n"];
        yield return ["inherited-getter-non-observation", InheritedGetterSource,
            "true false true 0\nfalse false\ntrue false true 0\nfalse false\n"];
        yield return ["locked-replacement", LockedReplacementSource, "false true locked false true false\n"];
        yield return ["computed-method-metadata-deletion", ComputedMethodSource, "true false true\ntrue false true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void OrdinaryFunctionMetadataUsesActualOwnDescriptors(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
