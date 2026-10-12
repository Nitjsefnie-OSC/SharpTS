using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public class ClassCallableDescriptorTests
{
    [Theory, ModeData]
    public void ComputedStaticMethodsHaveOwnDescriptorsWithoutInheritedDescriptors(ExecutionMode mode)
    {
        const string source = """
            const key = "sync";
            class C {
                static [key]() { return 42; }
                static async ["asyncMethod"]() { return 43; }
                static *["generator"]() { yield 44; }
                static async *["asyncGenerator"]() { yield 45; }
            }
            class Derived extends C {}
            class Generic<T> {
                static [key]() { return 42; }
                static async ["asyncMethod"]() { return 43; }
                static *["generator"]() { yield 44; }
                static async *["asyncGenerator"]() { yield 45; }
            }
            class GenericDerived<T> extends Generic<T> {}
            for (const pair of [[C, Derived], [Generic, GenericDerived]]) {
                for (const name of ["sync", "asyncMethod", "generator", "asyncGenerator"]) {
                    const owner: any = pair[0], child: any = pair[1];
                    const descriptor = Object.getOwnPropertyDescriptor(owner, name);
                    console.log(typeof descriptor.value, descriptor.writable,
                        descriptor.enumerable, descriptor.configurable);
                    console.log(Object.hasOwn(owner, name), Object.hasOwn(child, name),
                        Object.getOwnPropertyDescriptor(child, name) === undefined);
                }
            }
            console.log(Object.getOwnPropertyDescriptor(C, "sync").value());
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat("function true false true\ntrue false true\n", 8)) + "42\n",
            TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void NativeConstructorOwnMethodsExcludeClrNames(ExecutionMode mode)
    {
        const string source = """
            console.log(Object.hasOwn(Number, "Parse"), Object.hasOwn(Number, "TryParse"));
            console.log(Object.getOwnPropertyDescriptor(Number, "Parse") === undefined);
            console.log(Object.hasOwn(Number, "parseInt"), Object.hasOwn(Error, "isError"));
            Object.defineProperty(Error, "custom", { value: 1 });
            console.log(Object.hasOwn(Error, "custom"), Object.getOwnPropertyDescriptor(Error, "custom").value);
            """;
        Assert.Equal("false false\ntrue\ntrue true\ntrue 1\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void RepeatedClassExpressionsKeepComputedStaticOwnership(ExecutionMode mode)
    {
        const string source = """
            function make() { return class { static ["method"]() { return 42; } }; }
            for (const owner of [make(), make()]) {
                class Child extends owner {}
                console.log(typeof Object.getOwnPropertyDescriptor(owner, "method").value,
                    Object.hasOwn(owner, "method"), Object.hasOwn(Child, "method"),
                    Object.getOwnPropertyDescriptor(Child, "method") === undefined);
            }
            """;
        Assert.Equal("function true false true\nfunction true false true\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void InheritedStaticMethodsAreValuesWithoutOwnDescriptors(ExecutionMode mode)
    {
        const string source = """
            class C {
                static sync() { return 42; }
                static async asyncMethod() { return 43; }
                static *generator() { yield 44; }
                static async *asyncGenerator() { yield 45; }
            }
            class Derived extends C {}
            class Generic<T> {
                static sync() { return 42; }
                static async asyncMethod() { return 43; }
                static *generator() { yield 44; }
                static async *asyncGenerator() { yield 45; }
            }
            class GenericDerived<T> extends Generic<T> {}
            for (const pair of [[C, Derived], [Generic, GenericDerived]]) {
                for (const name of ["sync", "asyncMethod", "generator", "asyncGenerator"]) {
                    const owner: any = pair[0], child: any = pair[1];
                    console.log(typeof child[name], Object.hasOwn(owner, name), Object.hasOwn(child, name));
                    console.log(typeof Object.getOwnPropertyDescriptor(owner, name).value,
                        Object.getOwnPropertyDescriptor(child, name) === undefined);
                }
            }
            console.log(typeof TypeError.isError, Object.hasOwn(Error, "isError"), Object.hasOwn(TypeError, "isError"));
            console.log(Object.getOwnPropertyDescriptor(TypeError, "isError") === undefined);
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat("function true false\nfunction true\n", 8)) +
            "function true false\ntrue\n", TestHarness.Run(source, mode));
    }

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

    [Theory, ModeData]
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
