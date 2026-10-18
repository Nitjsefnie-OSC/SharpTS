using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class AccessorReturnValueTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["numeric-returns", """
            class Base { get value() { return 1; } }
            class Derived extends Base { set value(next: number) {} }
            function inferred(value: Base) { return value.value; }
            function annotated(value: Base): number { return value.value; }
            const arrow = (value: Base) => value.value;
            const block = (value: Base) => { return value.value; };
            class Reader { read(value: Base) { return value.value; } }
            const value: any = new Derived();
            console.log(value.value, inferred(value), annotated(value), arrow(value), block(value), new Reader().read(value));
            console.log(inferred(new Base()), annotated(new Base()), arrow(new Base()));
            """, "undefined undefined undefined undefined undefined undefined\n1 1 1\n"];
        yield return ["boolean-returns", """
            class Base { get value() { return true; } }
            class Derived extends Base { set value(next: boolean) {} }
            function inferred(value: Base) { return value.value; }
            function annotated(value: Base): boolean { return value.value; }
            const arrow = (value: Base) => value.value;
            const value: any = new Derived();
            console.log(inferred(value), annotated(value), arrow(value));
            console.log(inferred(new Base()), annotated(new Base()), arrow(new Base()));
            """, "undefined undefined undefined\ntrue true true\n"];
        yield return ["indexed-and-static", """
            class Base { get value() { return 1; } static get value() { return 2; } }
            class Derived extends Base { set value(next: number) {} static set value(next: number) {} }
            function indexed(value: Base) { return value["value"]; }
            function readStatic(value: typeof Base) { return value.value; }
            const instance: any = new Derived(), constructor: any = Derived;
            console.log(indexed(instance), readStatic(constructor));
            console.log(indexed(new Base()), readStatic(Base));
            """, "undefined undefined\n1 2\n"];
        yield return ["locals-and-wrappers", """
            class Base { get value() { return 1; } }
            class Derived extends Base { set value(next: number) {} }
            function local(value: Base) { const result: number = value.value; return result; }
            function assigned(value: Base) { let result = 3; result = value.value; return result; }
            function wrapped(value: Base, choose: boolean) { return choose ? (value.value as number) : 4; }
            function arithmetic(value: Base) { return value.value * 2; }
            const value: any = new Derived();
            console.log(local(value), assigned(value), wrapped(value, true), wrapped(value, false), arithmetic(value));
            console.log(local(new Base()), assigned(new Base()), wrapped(new Base(), true), arithmetic(new Base()));
            """, "undefined undefined undefined 4 NaN\n1 1 1 2\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void GetterlessAccessorValuesSurvivePrimitiveReturnBoundaries(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
