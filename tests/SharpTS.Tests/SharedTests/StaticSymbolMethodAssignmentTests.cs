using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class StaticSymbolMethodAssignmentTests
{
    public const string DescriptorIssueSource = """
        const key = Symbol('method');
        class C { static [key]() {} }
        const c: any = C;
        console.log(typeof c[key], Object.getOwnPropertyDescriptor(c, key) === undefined);
        """ + "\n";

    public const string DescriptorIdentitySource = """
        const first = Symbol("same"), second = Symbol("same");
        class Base {
            static [first]() { return this; }
            static [second]() { return 2; }
        }
        class Child extends Base {}
        const base: any = Base, child: any = Child;
        const descriptor = Object.getOwnPropertyDescriptor(base, first)!;
        const other = Object.getOwnPropertyDescriptor(base, second)!;
        console.log(descriptor.value === base[first], descriptor.value === child[first],
            descriptor.value !== other.value, descriptor.writable, descriptor.enumerable, descriptor.configurable);
        console.log(Object.getOwnPropertyDescriptor(child, first) === undefined,
            child[first]() === child, other.value());
        const expression: any = class { static [first]() { return 3; } };
        const expressionDescriptor = Object.getOwnPropertyDescriptor(expression, first)!;
        console.log(expressionDescriptor.value === expression[first], expressionDescriptor.writable,
            expressionDescriptor.enumerable, expressionDescriptor.configurable, expressionDescriptor.value());
        """ + "\n";

    public const string DescriptorReplacementSource = """
        const key = Symbol("method");
        class Base { static [key]() { return 1; } }
        class Child extends Base {}
        const base: any = Base, child: any = Child;
        const original = base[key];
        const replacement = function(this: any) { return this; };
        base[key] = replacement;
        const replaced = Object.getOwnPropertyDescriptor(base, key)!;
        console.log(replaced.value === replacement, replaced.value === base[key], original !== base[key],
            replaced.writable, replaced.enumerable, replaced.configurable, child[key]() === child);
        child[key] = () => 9;
        const shadow = Object.getOwnPropertyDescriptor(child, key)!;
        console.log(shadow.value === child[key], child[key](), shadow.writable, shadow.enumerable,
            shadow.configurable, base[key] === replacement, base[key]() === base);
        """ + "\n";

    public const string DescriptorGenericSource = """
        const key = Symbol("method");
        class Base<T> { static [key]() { return 7; } }
        class Child<T> extends Base<T> {}
        new Base<number>();
        const base: any = Base, child: any = Child;
        const original = base[key];
        const descriptor = Object.getOwnPropertyDescriptor(base, key)!;
        console.log(descriptor.value === original, original === child[key], descriptor.writable,
            descriptor.enumerable, descriptor.configurable, Object.getOwnPropertyDescriptor(child, key) === undefined);
        const replacement = () => 9;
        base[key] = replacement;
        new Base<string>();
        new Child<boolean>();
        const after = Object.getOwnPropertyDescriptor(base, key)!;
        console.log(after.value === replacement, base[key] === replacement, child[key] === replacement,
            child[key](), after.writable, after.enumerable, after.configurable,
            Object.getOwnPropertyDescriptor(child, key) === undefined);
        """ + "\n";

    public const string DescriptorMutationSource = """
        const key = Symbol("method");
        class Base<T> { static [key]() { return 7; } }
        new Base<number>();
        const base: any = Base;
        const original = base[key];
        console.log(Object.getOwnPropertySymbols(base).includes(key), Reflect.ownKeys(base).includes(key));
        Object.defineProperty(base, key, { writable: false });
        const locked = Object.getOwnPropertyDescriptor(base, key)!;
        console.log(locked.value === original, base[key] === original, locked.writable,
            locked.enumerable, locked.configurable, base[key]());
        console.log(delete base[key], base[key] === undefined,
            Object.getOwnPropertyDescriptor(base, key) === undefined, !Object.getOwnPropertySymbols(base).includes(key));
        new Base<string>();
        console.log(base[key] === undefined, Object.getOwnPropertyDescriptor(base, key) === undefined,
            !Reflect.ownKeys(base).includes(key));
        """ + "\n";

    public static IEnumerable<object[]> DescriptorCases()
    {
        yield return ["issue", DescriptorIssueSource, "function false\n"];
        yield return ["descriptors-and-inheritance", DescriptorIdentitySource, "true true true true false true\ntrue true 2\ntrue true false true 3\n"];
        yield return ["replacement-and-child-shadow", DescriptorReplacementSource, "true true true true false true true\ntrue 9 true true true true true\n"];
        yield return ["generic-owner-after-closed-initialization", DescriptorGenericSource, "true true true false true true\ntrue true true 9 true false true true\n"];
        yield return ["keys-partial-definition-and-delete", DescriptorMutationSource, "true true\ntrue true false false true 7\ntrue true true true\ntrue true true\n"];
    }

    public static IEnumerable<object[]> DescriptorModeCases()
        => DescriptorCases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(DescriptorModeCases))]
    public void DeclaredStaticSymbolMethodsHaveLiveOwnDescriptors(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

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
            class Field { static [(key as symbol)]() { return 4; } static [key] = 5; }
            const expression: any = class { static [(key as symbol)]() { return 6; } static [key] = 7; };
            const field: any = Field;
            console.log(field[key], Object.getOwnPropertyDescriptor(field, key)!.enumerable,
                expression[key], Object.getOwnPropertyDescriptor(expression, key)!.enumerable);
            """;
        Assert.Equal("true true false true\ntrue 2\n5 true 7 true\n",
            TestHarness.RunInterpreted(source));
    }

    [Theory, ModeData]
    public void AssignmentReplacesDeclaredMethodsAndShadowsInheritedMethods(ExecutionMode mode)
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
            TestHarness.Run(source, mode));
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
