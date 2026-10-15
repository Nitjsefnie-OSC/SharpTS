using SharpTS.Diagnostics;
using SharpTS.Parsing;
using SharpTS.TypeSystem;
using SharpTS.TypeSystem.Exceptions;
using Xunit;

namespace SharpTS.Tests.TypeCheckerTests;

public sealed class SuperInstanceAccessorTypeTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["declared-getter", """
            class Base { get value(): number { return 7; } }
            class Derived extends Base { read(): number { return super.value; } }
            """];
        yield return ["inferred-getter", """
            class Base { get value() { return 7; } }
            class Derived extends Base { read(): number { return super.value; } }
            """];
        yield return ["inherited-getter", """
            class Base { get value(): number { return 7; } }
            class Middle extends Base {}
            class Derived extends Middle { read(): number { return super.value; } }
            """];
        yield return ["nearest-getter", """
            class Root { get value(): any { return "root"; } }
            class Base extends Root { get value(): number { return 7; } }
            class Derived extends Base { read(): number { return super.value; } }
            """];
        yield return ["setter-only-shadows-getter", """
            class Root { get value(): any { return "root"; } }
            class Base extends Root { set value(next: number) {} }
            class Derived extends Base { read(): number { return super.value; } }
            """];
        yield return ["generic-chain", """
            class Root<T> { get value(): T { return undefined as any; } }
            class Base<U> extends Root<U> {}
            class Derived extends Base<number> { read(): number { return super.value; } }
            """];
        yield return ["nested-generic-substitution", """
            class Root<T> { get value(): T[] { return []; } }
            class Base<U> extends Root<U[]> {}
            class Derived extends Base<number> { read(): number[][] { return super.value; } }
            """];
        yield return ["protected-generic-getter", """
            class Root<T> { protected get value(): T { return undefined as any; } }
            class Base<U> extends Root<U> {}
            class Derived extends Base<number> { read(): number { return super.value; } }
            """];
        yield return ["auto-accessor", """
            class Base { accessor value: number = 7; }
            class Derived extends Base { read(): number { return super.value; } }
            """];
        yield return ["protected-auto-accessor", """
            class Base { protected accessor value: number = 7; }
            class Derived extends Base { read(): number { return super.value; } }
            """];
        yield return ["expression-getter-inference", """
            const Base = class { get value() { return 7; } };
            class Derived extends Base { read(): number { return super.value; } }
            """];
        yield return ["derived-expression-getter", """
            const Base = class { get value() { return 7; } };
            const Derived = class extends Base { read(): number { return super.value; } };
            """];
        yield return ["expression-unannotated-setter", """
            const C = class { set value(next) { next(); } };
            new C().value = () => {};
            """];
        yield return ["expression-callable-getter-used-by-method", """
            const C = class {
                get action() { return () => 7; }
                read() { return this.action(); }
            };
            new C().read();
            """];
        yield return ["public-getter-before-private-setter", """
            class Base {
                public get value(): number { return 7; }
                private set value(next: number) {}
            }
            class Derived extends Base { read(): number { return super.value; } }
            """];
        yield return ["public-getter-after-private-setter", """
            class Base {
                private set value(next: number) {}
                public get value(): number { return 7; }
            }
            class Derived extends Base { read(): number { return super.value; } }
            """];
        yield return ["ambient-getter", """
            declare class Base<T> { get value(): T; }
            class Derived extends Base<number> { read(): number { return super.value; } }
            """];
        yield return ["ambient-setter-only", """
            declare class Base<T> { set value(next: T); }
            class Derived extends Base<number> { read(): number { return super.value; } }
            """];
        yield return ["shadowed-unrelated-private-owner", """
            class Base { private get value(): number { return 1; } }
            function local() {
                class Base { get value(): number { return 2; } }
                class Derived extends Base { read(): number { return super.value; } }
                return Derived;
            }
            """];
    }

    public static IEnumerable<object[]> InvalidCases()
    {
        yield return ["missing", """
            class Base { get value(): number { return 7; } }
            class Derived extends Base { read() { return super.missing; } }
            """, "TS2339"];
        yield return ["instance-static-only", """
            class Base { static get value(): number { return 7; } }
            class Derived extends Base { read() { return super.value; } }
            """, "TS2339"];
        yield return ["static-instance-only", """
            class Base { get value(): number { return 7; } }
            class Derived extends Base { static read() { return super.value; } }
            """, "TS2339"];
        yield return ["declared-getter-is-not-any", """
            class Base { get value(): number { return 7; } }
            class Derived extends Base { read() { const result: string = super.value; } }
            """, "TS2322"];
        yield return ["inferred-getter-is-not-any", """
            class Base { get value() { return 7; } }
            class Derived extends Base { read() { const result: string = super.value; } }
            """, "TS2322"];
        yield return ["nearest-getter-is-not-any", """
            class Root { get value(): any { return "root"; } }
            class Base extends Root { get value(): number { return 7; } }
            class Derived extends Base { read() { const result: string = super.value; } }
            """, "TS2322"];
        yield return ["setter-only-is-not-ancestor-any", """
            class Root { get value(): any { return "root"; } }
            class Base extends Root { set value(next: number) {} }
            class Derived extends Base { read() { const result: string = super.value; } }
            """, "TS2322"];
        yield return ["generic-getter-substitution", """
            class Root<T> { get value(): T { return undefined as any; } }
            class Base<U> extends Root<U> {}
            class Derived extends Base<number> { read() { const result: string = super.value; } }
            """, "TS2322"];
        yield return ["nested-generic-getter-substitution", """
            class Root<T> { get value(): T[] { return []; } }
            class Base<U> extends Root<U[]> {}
            class Derived extends Base<number> { read() { const result: string[][] = super.value; } }
            """, "TS2322"];
        yield return ["expression-getter-is-not-any", """
            const Base = class { get value() { return 7; } };
            class Derived extends Base { read() { const result: string = super.value; } }
            """, "TS2322"];
        yield return ["derived-expression-getter-is-not-any", """
            const Base = class { get value() { return 7; } };
            const Derived = class extends Base { read() { const result: string = super.value; } };
            """, "TS2322"];
        yield return ["expression-callable-getter-is-not-any", """
            const C = class {
                get action() { return () => 7; }
                read() { return this.action(); }
            };
            const result: string = new C().action();
            """, "TS2322"];
        yield return ["ambient-setter-type-is-not-any", """
            declare class Base<T> { set value(next: T); }
            class Derived extends Base<number> { read() { const result: string = super.value; } }
            """, "TS2322"];
        yield return ["private-declaration-getter", """
            class Base { private get value(): number { return 7; } }
            class Derived extends Base { read() { return super.value; } }
            """, "TS2341"];
        yield return ["private-expression-getter", """
            const Base = class { private get value(): number { return 7; } };
            class Derived extends Base { read() { return super.value; } }
            """, "TS2341"];
        yield return ["private-auto-accessor", """
            class Base { private accessor value: number = 7; }
            class Derived extends Base { read() { return super.value; } }
            """, "TS2341"];
        yield return ["private-ambient-getter", """
            declare class Base { private get value(): number; }
            class Derived extends Base { read() { return super.value; } }
            """, "TS2341"];
        yield return ["private-getter-before-public-setter", """
            class Base {
                private get value(): number { return 7; }
                public set value(next: number) {}
            }
            class Derived extends Base { read() { return super.value; } }
            """, "TS2341"];
        yield return ["private-getter-after-public-setter", """
            class Base {
                public set value(next: number) {}
                private get value(): number { return 7; }
            }
            class Derived extends Base { read() { return super.value; } }
            """, "TS2341"];
        yield return ["private-setter-only", """
            class Base { private set value(next: number) {} }
            class Derived extends Base { read() { return super.value; } }
            """, "TS2341"];
        yield return ["nearest-private-accessor", """
            class Root { get value(): any { return "root"; } }
            class Base extends Root { private get value(): number { return 7; } }
            class Derived extends Base { read() { return super.value; } }
            """, "TS2341"];
        yield return ["same-spelled-private-owner", """
            const Parent = class Same { private get value(): number { return 7; } };
            const Child = class Same extends Parent { read() { return super.value; } };
            """, "TS2341"];
        yield return ["instance-field", """
            class Base { value: number = 7; }
            class Derived extends Base { read() { return super.value; } }
            """, "TS2855"];
        yield return ["nearer-field-masks-getter", """
            class Root { get value(): any { return "root"; } }
            class Base extends Root { value: number = 7; }
            class Derived extends Base { read() { return super.value; } }
            """, "TS2855"];
        yield return ["callable-getter-argument", """
            class Base { get value(): (input: number) => number { return input => input; } }
            class Derived extends Base { read() { return super.value("wrong"); } }
            """, "TS2345"];
        yield return ["numeric-getter-is-not-callable", """
            class Base { get value(): number { return 7; } }
            class Derived extends Base { read() { return super.value(); } }
            """, "TS2349"];
    }

    [Theory, MemberData(nameof(Cases))]
    public void InheritedAccessorReadsKeepTheirSelectedTypeAndVisibility(string name, string source)
    {
        var diagnostics = Check(source);
        Assert.True(diagnostics.All(d => d.Severity != DiagnosticSeverity.Error),
            $"{name}: {string.Join(';', diagnostics.Select(d => d.TsCode + ": " + d.Message))}");
    }

    [Theory, MemberData(nameof(InvalidCases))]
    public void MissingInaccessibleAndIncorrectlyTypedReadsRemainErrors(string name, string source, string code)
    {
        var diagnostics = Check(source);
        Assert.True(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error && d.TsCode == code),
            $"{name}: expected {code}; received {string.Join(';', diagnostics.Select(d => d.TsCode + ": " + d.Message))}");
    }

    [Theory]
    [InlineData("class Base { private get value(): (input: number) => number { return input => input; } } class Derived extends Base { read() { return super.value(missingArgument); } }", "TS2341")]
    [InlineData("class Base {} class Derived extends Base { read() { return super.missing(missingArgument); } }", "TS2339")]
    public void CalleeLookupRejectsPrivateOrMissingMembersBeforeArguments(string source, string code)
    {
        var error = Assert.ThrowsAny<TypeCheckException>(() => new TypeChecker().Check(Parse(source)));
        Assert.Equal(code, error.Diagnostic.TsCode);
        Assert.DoesNotContain("missingArgument", error.Message);
    }

    private static IReadOnlyList<Diagnostic> Check(string source)
        => new TypeChecker(maxErrors: 50).CheckWithRecovery(Parse(source)).Diagnostics;

    private static List<Stmt> Parse(string source)
        => new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();
}
