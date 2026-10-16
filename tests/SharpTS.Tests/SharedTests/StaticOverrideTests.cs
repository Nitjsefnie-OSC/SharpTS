using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class StaticOverrideTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original-getter", """
            class Base { static get x(): number { return 1; } }
            class Child extends Base { static override get x(): number { return 2; } }
            console.log(Child.x);
            """, "2\n"];
        yield return ["original-method", """
            class Animal { static speak(): string { return '...'; } }
            class Dog extends Animal { static override speak(): string { return 'Woof'; } }
            console.log(Dog.speak());
            """, "Woof\n"];
        yield return ["getter-and-setter", """
            const state: any = { value: 1 };
            class Base {
                static get value(): number { return 1; }
                static set value(next: number) { state.value = next; }
            }
            class Child extends Base {
                static override get value(): number { return state.value * 2; }
                static override set value(next: number) { state.value = next + 1; }
            }
            Child.value = 4;
            console.log(Child.value, Base.value);
            """, "10 1\n"];
        yield return ["grandparent-static-member", """
            class Base { static value(): string { return 'base'; } }
            class Middle extends Base {}
            class Child extends Middle { static override value(): string { return 'child'; } }
            console.log(Child.value(), Middle.value());
            """, "child base\n"];
        yield return ["access-modifiers", """
            class Base {
                protected static value(): string { return 'base'; }
            }
            class Child extends Base {
                protected static override value(): string { return 'child'; }
                static read(): string { return Child.value(); }
            }
            console.log(Child.read());
            """, "child\n"];
        yield return ["actual-static-receiver", """
            class Base { static value(): any { return this; } }
            class Child extends Base {
                static override value(): any { return super.value(); }
            }
            class Leaf extends Child {}
            console.log(Child.value() === Child, Leaf.value() === Leaf);
            """, "true true\n"];
        yield return ["generic-base", """
            class Base<T> { static value(): string { return 'base'; } }
            class Child extends Base<number> { static override value(): string { return 'child'; } }
            console.log(Child.value(), Base.value());
            """, "child base\n"];
        yield return ["async-and-generator-methods", """
            class Base {
                static async read(): Promise<string> { return 'base'; }
                static *values(): any { yield 'base'; }
            }
            class Child extends Base {
                static override async read(): Promise<string> { return 'child'; }
                static override *values(): any { yield 'value'; }
            }
            async function run() {
                console.log(await Child.read());
                console.log(Child.values().next().value);
            }
            run();
            """, "child\nvalue\n"];
        yield return ["compatible-accessor-kinds", """
            const state: any = { value: 0 };
            class GetterBase { static get value(): number { return 1; } }
            class SetterChild extends GetterBase {
                static override set value(next: number) { state.value = next; }
            }
            SetterChild.value = 3;
            console.log(state.value, SetterChild.value === undefined, GetterBase.value);
            class SetterBase { static set value(next: number) {} }
            class GetterChild extends SetterBase { static override get value(): number { return 2; } }
            console.log(GetterChild.value, SetterBase.value === undefined);
            """, "3 true 1\n2 true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases()
        .SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void StaticOverridesUseTheStaticInheritanceFacet(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, output) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            Assert.Equal(expected, output);
        }
        else
        {
            Assert.Equal(expected, TestHarness.Run(source, mode));
        }
    }
}
