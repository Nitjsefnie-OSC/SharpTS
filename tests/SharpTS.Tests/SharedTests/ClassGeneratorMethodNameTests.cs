using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassGeneratorMethodNameTests
{
    public const string Source = """
        class C { *id() {} static *id() {} }
        const instance: any = new C(), constructorValue: any = C;
        for (const method of [instance.id, constructorValue.id]) {
            const descriptor = Object.getOwnPropertyDescriptor(method, "name")!;
            console.log(JSON.stringify(method.name), JSON.stringify(descriptor.value),
                descriptor.writable, descriptor.enumerable, descriptor.configurable);
            console.log(delete method.name, Object.hasOwn(method, "name"));
        }
        """;

    public const string Expected = "\"id\" \"id\" false false true\ntrue false\n"
        + "\"id\" \"id\" false false true\ntrue false\n";

    public const string VariantsSource = """
        let calls = 0;
        class C { *id(value: any) { calls++; } static *id(value: any) { calls++; } }
        class D { async *id(value: any) { calls++; } static async *id(value: any) { calls++; } }
        const E = class { *id(value: any) { calls++; } static *id(value: any) { calls++; } };
        const F = class { async *id(value: any) { calls++; } static async *id(value: any) { calls++; } };
        const c: any = new C(), d: any = new D(), e: any = new E(), f: any = new F();
        const cc: any = C, dc: any = D, ec: any = E, fc: any = F;
        for (const method of [c.id, cc.id, d.id, dc.id, e.id, ec.id, f.id, fc.id]) {
            const length = Object.getOwnPropertyDescriptor(method, "length")!;
            console.log(method.name, length.value, length.writable, length.enumerable, length.configurable);
            console.log(delete method.name, delete method.length,
                Object.hasOwn(method, "name"), Object.hasOwn(method, "length"));
        }
        for (const method of [c.id, cc.id, d.id, dc.id, e.id, ec.id, f.id, fc.id]) {
            console.log(JSON.stringify(method.name), method.length,
                Object.getOwnPropertyDescriptor(method, "name") === undefined,
                Object.getOwnPropertyDescriptor(method, "length") === undefined);
        }
        console.log(calls);
        """;

    public static string VariantsExpected => string.Concat(Enumerable.Repeat(
        "id 1 false false true\ntrue true false false\n", 8))
        + string.Concat(Enumerable.Repeat("\"\" 0 true true\n", 8)) + "0\n";

    [Theory, ModeData]
    public void OrdinaryGeneratorClassMethodsHaveConfigurableDeclaredNames(ExecutionMode mode)
        => Assert.Equal(Expected, TestHarness.Run(Source, mode));

    [Theory, ModeData]
    public void ReadingGeneratorMethodAgainPreservesDeletedMetadata(ExecutionMode mode)
        => Assert.Equal(VariantsExpected, TestHarness.Run(VariantsSource, mode));

    public const string IdentitySource = """
        class C { *id(value: any) {} }
        const object: any = new C(), method: any = object.id;
        console.log(object.id === method);
        console.log(delete method.name, delete method.length);
        console.log(Object.hasOwn(method, "name"), Object.hasOwn(method, "length"));
        console.log(JSON.stringify(object.id.name), object.id.length, Object.hasOwn(object.id, "name"));
        """;

    [Theory, ModeData]
    public void RepeatedGeneratorReadsRetainFunctionIdentity(ExecutionMode mode)
        => Assert.Equal("true\ntrue true\nfalse false\n\"\" 0 false\n", TestHarness.Run(IdentitySource, mode));

    [Theory, ModeData]
    public void CanonicalGeneratorMethodKeepsDynamicReceiver(ExecutionMode mode)
    {
        const string source = """
            class C {
                constructor(public tag: number) {}
                *id(value: number) { yield this.tag + value; }
            }
            const first: any = new C(1), second: any = new C(2), method: any = first.id;
            console.log(method === first.id, method === second.id, method === C.prototype.id);
            console.log(first.id(3).next().value, second.id(3).next().value, method.call(second, 3).next().value);
            Object.defineProperty(method, "name", { value: "renamed", configurable: true });
            console.log(first.id.name, second.id.name, C.prototype.id.name);
            """;
        Assert.Equal("true true true\n4 5 5\nrenamed renamed renamed\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void DeletedGeneratorMetadataReadsInheritedDescriptors(ExecutionMode mode)
    {
        const string source = """
            class C { *id(value: any) {} }
            const method: any = new C().id, state = { reads: 0 };
            delete method.name; delete method.length;
            Object.defineProperty(Function.prototype, "name", {
                get() { state.reads++; return this === method ? "inherited" : "other"; }, configurable: true
            });
            Object.defineProperty(Function.prototype, "length", { value: 9, configurable: true });
            console.log(method.name, method.length, state.reads);
            Object.defineProperty(method, "name", { value: undefined, configurable: true });
            Object.defineProperty(method, "length", { value: undefined, configurable: true });
            console.log(method.name === undefined, method.length === undefined, state.reads);
            """;
        Assert.Equal("inherited 9 1\ntrue true 1\n", TestHarness.Run(source, mode));
    }
}
