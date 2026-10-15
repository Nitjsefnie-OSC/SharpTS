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

    [Fact]
    public void InterpreterReadingGeneratorMethodAgainPreservesDeletedMetadata()
        => Assert.Equal(VariantsExpected, TestHarness.Run(VariantsSource, ExecutionMode.Interpreted));
}
