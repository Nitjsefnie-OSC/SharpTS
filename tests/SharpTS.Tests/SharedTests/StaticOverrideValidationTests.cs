using SharpTS.Tests.Infrastructure;
using SharpTS.TypeSystem.Exceptions;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class StaticOverrideValidationTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["missing-static-method", "class A {} class B extends A { static override value() { return 1; } }", false];
        yield return ["instance-method-is-not-static", "class A { value() { return 1; } } class B extends A { static override value() { return 2; } }", false];
        yield return ["instance-getter-is-not-static", "class A { get value(): number { return 1; } } class B extends A { static override get value(): number { return 2; } }", false];
        yield return ["instance-setter-is-not-static", "class A { set value(next: number) {} } class B extends A { static override set value(next: number) {} }", false];
        yield return ["static-method-is-not-instance", "class A { static value() { return 1; } } class B extends A { override value() { return 2; } }", false];
        yield return ["incompatible-return", "class A { static value(): number { return 1; } } class B extends A { static override value(): string { return 'bad'; } }", false];
        yield return ["incompatible-parameter", "class A { static value(next: number): number { return next; } } class B extends A { static override value(next: string): number { return 1; } }", false];
        yield return ["incompatible-getter", "class A { static get value(): number { return 1; } } class B extends A { static override get value(): string { return 'bad'; } }", false];
        yield return ["incompatible-setter", "class A { static set value(next: number) {} } class B extends A { static override set value(next: string) {} }", false];
        yield return ["getter-to-incompatible-setter", "class A { static get value(): number { return 1; } } class B extends A { static override set value(next: string) {} }", false];
        yield return ["setter-to-incompatible-getter", "class A { static set value(next: number) {} } class B extends A { static override get value(): string { return 'bad'; } }", false];
        yield return ["generic-missing-static", "class A<T> {} class B<T> extends A<T> { static override value() { return 1; } }", false];
        yield return ["no-superclass", "class B { static override value() { return 1; } }", true];
        yield return ["modifier-order", "class A { static value() { return 1; } } class B extends A { override static value() { return 2; } }", true];
        yield return ["abstract-static", "abstract class A { static value() { return 1; } } abstract class B extends A { abstract static override value(): number; }", true];
    }

    public static IEnumerable<object[]> ModeCases() => Cases()
        .SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void InvalidStaticOverridesKeepTheirDiagnostics(
        string name, string source, bool parseError, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        var error = Assert.ThrowsAny<Exception>(() => TestHarness.Run(source, mode));
        if (parseError)
            Assert.Contains("Parse Error", error.Message);
        else
            Assert.IsAssignableFrom<TypeCheckException>(error);
    }
}
