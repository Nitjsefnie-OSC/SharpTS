using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class StaticSymbolMethodAssignmentTests
{
    [Fact]
    public void ReplacementKeepsMethodAttributesWhileShadowsAndFieldsAreEnumerable()
    {
        const string source = """
            const key = Symbol("method");
            class Base { static [key]() { return 1; } }
            class Child extends Base {}
            const base: any = Base, child: any = Child;
            base[key] = () => 2;
            const descriptor: any = Object.getOwnPropertyDescriptor(base, key);
            console.log(descriptor.value === base[key], descriptor.writable,
                descriptor.enumerable, descriptor.configurable);
            child[key] = 3;
            console.log(Object.getOwnPropertyDescriptor(child, key)!.enumerable, base[key]());
            class Field { static [key]() { return 4; } static [key] = 5; }
            const expression: any = class { static [key]() { return 6; } static [key] = 7; };
            const field: any = Field;
            console.log(field[key], Object.getOwnPropertyDescriptor(field, key)!.enumerable,
                expression[key], Object.getOwnPropertyDescriptor(expression, key)!.enumerable);
            """;
        Assert.Equal("true true false true\ntrue 2\n5 true 7 true\n",
            TestHarness.RunInterpreted(source));
    }

    [Fact]
    public void AssignmentReplacesDeclaredMethodsAndShadowsInheritedMethods()
    {
        const string source = """
            const key = Symbol("method");
            class Base { static [key]() { return 1; } }
            class Child extends Base {}
            class Override extends Base { static [key]() { return 3; } }
            const base: any = Base, child: any = Child, override: any = Override;
            const original = base[key];
            console.log(original === base[key], original === child[key]);
            child[key] = function() { return this === child ? 2 : 9; };
            console.log(child[key](), child[key] === original, base[key]());
            base[key] = () => 4;
            console.log(base[key](), base[key] === original, child[key](), override[key]());
            base[key] = null;
            console.log(base[key] === null, child[key](), override[key]());
            base[key] = undefined;
            console.log(base[key] === undefined);
            """;
        Assert.Equal("true true\n2 false 1\n4 false 2 3\ntrue 2 3\ntrue\n",
            TestHarness.RunInterpreted(source));
    }

    [Theory, ModeData]
    public void AssignmentReplacesOwnDeclaredMethod(ExecutionMode mode)
    {
        const string source = """
            const key = Symbol("method");
            class C { static [key]() { return 1; } }
            const klass: any = C, original = klass[key];
            klass[key] = () => 2;
            console.log(klass[key](), klass[key] === original);
            """;
        Assert.Equal("2 false\n", TestHarness.Run(source, mode));
    }
}
