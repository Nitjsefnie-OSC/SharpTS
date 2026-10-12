using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public class ClassCallableDescriptorTests
{
    [Theory, ModeData]
    public void StaticMethodsHaveOwnNonEnumerableDescriptors(ExecutionMode mode)
    {
        const string source = """
            class C {
                static sync() { return 42; }
                static async asyncMethod() { return 43; }
                static *generator() { yield 44; }
                static async *asyncGenerator() { yield 45; }
                #private;
            }
            for (const name of ["sync", "asyncMethod", "generator", "asyncGenerator"]) {
                const descriptor = Object.getOwnPropertyDescriptor(C, name);
                console.log(typeof descriptor.value, descriptor.writable,
                    descriptor.enumerable, descriptor.configurable);
            }
            console.log(Object.getOwnPropertyDescriptor(C, "sync").value());
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat("function true false true\n", 4)) + "42\n",
            TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ClassCallsRejectWithoutExecutingConstructors(ExecutionMode mode)
    {
        const string source = """
            let calls = 0;
            class Base { constructor() { calls++; } }
            class Derived extends Base {}
            class Explicit extends Base { constructor() { super(); } }
            class ErrorChild extends Error { constructor() { super(); calls++; } }
            class ArrayChild extends Array<any> { constructor() { super(); calls++; } }
            for (const ctor of [Base, Derived, Explicit, ErrorChild, ArrayChild]) {
                try { (ctor as any)(); } catch (error) { console.log(error instanceof TypeError); }
                try { ctor.apply({}, []); } catch (error) { console.log(error instanceof TypeError); }
                try { ctor.call({}); } catch (error) { console.log(error instanceof TypeError); }
                try { Reflect.apply(ctor, {}, []); } catch (error) { console.log(error instanceof TypeError); }
                const bound = ctor.bind({});
                try { bound(); } catch (error) { console.log(error instanceof TypeError); }
            }
            console.log(calls);
            new Derived();
            new Explicit();
            new ErrorChild();
            new ArrayChild();
            console.log(calls);
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat("true\n", 25)) + "0\n4\n",
            TestHarness.Run(source, mode));
    }

    // Compiled controls have separate failures in #2002, #2004, and #2005.
    [Theory, InterpretedOnlyData]
    public void NativeConstructorsRemainCallableAndInheritedMethodsAreNotOwn(ExecutionMode mode)
    {
        const string source = """
            class C { static async method() { return 42; } }
            class Derived extends C {}
            console.log(Object.getOwnPropertyDescriptor(Derived, "method") === undefined);
            console.log(Error.call(null, "message") instanceof Error);
            console.log(TypeError.apply(null, ["message"]) instanceof TypeError);
            class Constructed { constructor(value: number) { console.log(value); } }
            const Bound: any = Constructed.bind({}, 43);
            new Bound();
            """;
        Assert.Equal("true\ntrue\ntrue\n43\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ClassCallArgumentsRunBeforeRejection(ExecutionMode mode)
    {
        const string source = """
            let evaluated = 0;
            class C { constructor(value: number) { console.log(value); } }
            function argument() { evaluated++; return 42; }
            try { (C as any)(argument()); } catch (error) { console.log(error instanceof TypeError); }
            try { (C as any)(...[argument()]); } catch (error) { console.log(error instanceof TypeError); }
            console.log(evaluated);
            async function test() {
                await Promise.resolve();
                try { (C as any)(argument()); } catch (error) { console.log(error instanceof TypeError); }
                console.log(evaluated);
            }
            test();
            """;
        Assert.Equal("true\ntrue\n2\ntrue\n3\n", TestHarness.Run(source, mode));
    }
}
