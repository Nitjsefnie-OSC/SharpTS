using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassSelfBindingShadowTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["declaration", """
            class C {
                read(C: any) { return C; }
                local(value: any) { const C = value; return C; }
                block(value: any) { { const C = value; return C; } }
                parameterArrow(C: any) { return () => C; }
                localArrow(value: any) { const C = value; return () => C; }
                self() { return C; }
                static read(C: any) { return C; }
                static local(value: any) { const C = value; return C; }
                static self() { return C; }
            }
            const value: any = {}, instance = new C();
            console.log(instance.read(value) === value, instance.local(value) === value,
                instance.block(value) === value);
            console.log(instance.parameterArrow(value)() === value, instance.localArrow(value)() === value);
            console.log(instance.self() === C, C.read(value) === value, C.local(value) === value, C.self() === C);
            """, "true true true\ntrue true\ntrue true true true\n"];

        yield return ["expression", """
            const Owner: any = class C {
                read(C: any) { return C; }
                local(value: any) { const C = value; return C; }
                parameterArrow(C: any) { return () => C; }
                localArrow(value: any) { const C = value; return () => C; }
                self() { return C; }
                static read(C: any) { return C; }
                static self() { return C; }
            };
            const value: any = {}, instance = new Owner();
            console.log(instance.read(value) === value, instance.local(value) === value);
            console.log(instance.parameterArrow(value)() === value, instance.localArrow(value)() === value);
            console.log(instance.self() === Owner, Owner.read(value) === value, Owner.self() === Owner);
            """, "true true\ntrue true\ntrue true true\n"];

        // Function-local method-local arrow captures have their own regression (#2041).
        // Direct locals exercise class-self shadowing independently.
        yield return ["nested-declaration", """
            function make() {
                class C {
                    read(C: any) { return C; }
                    local(value: any) { const C = value; return C; }
                    self() { return C; }
                }
                return { constructor: C, instance: new C() };
            }
            const value: any = {}, made: any = make(), instance = made.instance;
            console.log(instance.read(value) === value, instance.local(value) === value,
                instance.self() === made.constructor);
            """, "true true true\n"];

        yield return ["outer-collision", """
            function make(C: any) {
                return class C {
                    read(C: any) { return C; }
                    local(value: any) { const C = value; return C; }
                    self() { return C; }
                    static self() { return C; }
                };
            }
            const value: any = {}, First: any = make(value), Second: any = make(value);
            const first = new First(), second = new Second();
            console.log(first.read(value) === value, first.local(value) === value);
            console.log(first.self() === First, First.self() === First,
                second.self() === Second, Second.self() === Second, First !== Second);
            """, "true true\ntrue true true true true\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void MethodBindingsShadowClassSelfNames(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
