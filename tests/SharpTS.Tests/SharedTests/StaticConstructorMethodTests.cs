using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public class StaticConstructorMethodTests
{
    [Theory, ModeData]
    public void ClassExpression_StaticConstructorMethodIsOwnPropertyAndPreservesInstanceConstructor(ExecutionMode mode)
    {
        var source = """
            const C = class {
                value: number;
                static constructor() { return 42; }
                constructor(value: number) { this.value = value; }
            };
            const ctor: any = C;
            console.log(ctor.hasOwnProperty("constructor"));
            console.log(ctor.prototype.hasOwnProperty("constructor"));
            console.log(ctor.prototype.constructor === ctor);
            console.log(ctor.prototype.constructor !== ctor.constructor);
            console.log(ctor.constructor());
            console.log(new C(7).value);
            """;

        Assert.Equal("true\ntrue\ntrue\ntrue\n42\n7\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ClassExpression_StaticAsyncConstructorMethodReturnsPromiseAndPreservesInstanceConstructor(ExecutionMode mode)
    {
        var source = """
            const C = class {
                value: number;
                static async constructor() { return await Promise.resolve(42); }
                constructor(value: number) { this.value = value; }
            };
            const ctor: any = C;
            console.log(ctor.hasOwnProperty("constructor"));
            console.log(ctor.prototype.hasOwnProperty("constructor"));
            console.log(ctor.prototype.constructor === ctor);
            console.log(ctor.prototype.constructor !== ctor.constructor);
            console.log(new C(7).value);
            async function run() { console.log(await ctor.constructor()); }
            run();
            """;

        Assert.Equal("true\ntrue\ntrue\ntrue\n7\n42\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ClassDeclaration_StaticConstructorMethodIsOwnPropertyAndPreservesInstanceConstructor(ExecutionMode mode)
    {
        var source = """
            class C {
                value: number;
                static constructor() { return 42; }
                constructor(value: number) { this.value = value; }
            }
            const ctor: any = C;
            console.log(ctor.hasOwnProperty("constructor"));
            console.log(ctor.prototype.hasOwnProperty("constructor"));
            console.log(ctor.prototype.constructor === ctor);
            console.log(ctor.prototype.constructor !== ctor.constructor);
            console.log(ctor.constructor());
            console.log(new C(7).value);
            """;

        Assert.Equal("true\ntrue\ntrue\ntrue\n42\n7\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ClassDeclaration_StaticAsyncConstructorMethodReturnsPromiseAndPreservesInstanceConstructor(ExecutionMode mode)
    {
        var source = """
            class C {
                value: number;
                static async constructor() { return await Promise.resolve(42); }
                constructor(value: number) { this.value = value; }
            }
            const ctor: any = C;
            console.log(ctor.hasOwnProperty("constructor"));
            console.log(ctor.prototype.hasOwnProperty("constructor"));
            console.log(ctor.prototype.constructor === ctor);
            console.log(ctor.prototype.constructor !== ctor.constructor);
            console.log(new C(7).value);
            async function run() { console.log(await ctor.constructor()); }
            run();
            """;

        Assert.Equal("true\ntrue\ntrue\ntrue\n7\n42\n", TestHarness.Run(source, mode));
    }

    // The interpreter's GetOwnStaticPropertyDescriptor currently omits all declared
    // static methods. Keep the emitted property-descriptor contract covered here;
    // presence, calls and constructor identity above still run in both modes.
    [Theory, CompiledOnlyData]
    public void StaticConstructorMethodsHaveTheSameDescriptorsAsOtherStaticMethods(ExecutionMode mode)
    {
        var source = """
            const C = class {
                static constructor() { return 42; }
                static method() { return 7; }
                constructor() {}
            };
            class D {
                static constructor() { return 42; }
                static method() { return 7; }
                constructor() {}
            }
            for (const ctor of [C, D] as any[]) {
                for (const name of ["constructor", "method"]) {
                    const descriptor = Object.getOwnPropertyDescriptor(ctor, name);
                    console.log(descriptor.value === ctor[name]);
                    console.log(descriptor.writable, descriptor.enumerable, descriptor.configurable);
                }
            }
            """;

        Assert.Equal(string.Concat(Enumerable.Repeat("true\ntrue false true\n", 4)), TestHarness.Run(source, mode));
    }
}
