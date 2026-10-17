using SharpTS.Diagnostics;
using SharpTS.Modules;
using SharpTS.Parsing;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.TypeCheckerTests;

public sealed class AccessorVisibilityTests
{
    // Rejected programs are checked only; keyword visibility has no runtime brand.
    public static IEnumerable<object[]> RejectedCases()
    {
        yield return ["issue-private-instance", """
            class C { private get value() { return 7; } }
            const value: number = new C().value;
            console.log(value);
            """, "TS2341", 2];

        yield return ["inherited-private", """
            class Base { private get value(): number { return 7; } }
            class Derived extends Base {}
            const value: number = new Derived().value;
            """, "TS2341", 3];

        yield return ["generic-private", """
            class Box<T> { private get value(): T { return undefined as any; } }
            const value: number = new Box<number>().value;
            """, "TS2341", 2];

        yield return ["expression-private", """
            const C = class { private get value(): number { return 7; } };
            const value: number = new C().value;
            """, "TS2341", 2];

        yield return ["same-name-distinct-owner", """
            const A = class Same { private get value(): number { return 7; } };
            const B = class Same { read(other: InstanceType<typeof A>): number { return other.value; } };
            """, "TS2341", 2];

        yield return ["protected-outside", """
            class C { protected get value(): number { return 7; } }
            const value: number = new C().value;
            """, "TS2445", 2];

        yield return ["ordinary-generic-inherited-private", """
            class Root<T> { private get value(): T { return undefined as any; } }
            class Middle<U> extends Root<U> {}
            class Leaf<V> extends Middle<V> {}
            declare const receiver: Leaf<number>;
            const result: number = receiver.value;
            """, "TS2341", 5];

        yield return ["ordinary-generic-inherited-type", """
            class Root<T> { public get value(): T { return undefined as any; } }
            class Middle<U> extends Root<U> {}
            class Leaf<V> extends Middle<V> {}
            declare const receiver: Leaf<number>;
            const result: string = receiver.value;
            """, "TS2322", 5];

        yield return ["nearest-setter-only-wrong-type", """
            class Base { get p(): any { return 1; } }
            class Child extends Base { set p(value: string) {} }
            const result: number = new Child().p;
            """, "TS2322", 3];

        yield return ["protected-base-read", """
            class Base { protected get value(): number { return 7; } }
            class Derived extends Base { read(other: Base): number { return other.value; } }
            """, "TS2446", 2];

        yield return ["protected-sibling-read", """
            class Base { protected get value(): number { return 7; } }
            class Sibling extends Base {}
            class Derived extends Base { read(other: Sibling): number { return other.value; } }
            """, "TS2446", 3];

        yield return ["protected-base-write", """
            class Base { public get value(): number { return 7; } protected set value(next: number) {} }
            class Derived extends Base { write(other: Base): void { other.value = 2; } }
            """, "TS2446", 2];

        yield return ["protected-sibling-write", """
            class Base { public get value(): number { return 7; } protected set value(next: number) {} }
            class Sibling extends Base {}
            class Derived extends Base { write(other: Sibling): void { other.value = 2; } }
            """, "TS2446", 3];

        yield return ["mixed-private-setter-write", """
            class C { public get value(): number { return 7; } private set value(next: number) {} }
            new C().value = 2;
            """, "TS2341", 2];

        yield return ["instance-setter-first-write", """
            class C { private set value(next: number) {} public get value(): number { return 7; } }
            new C().value = 2;
            """, "TS2341", 2];

        yield return ["inherited-generic-setter-write", """
            class Base<T> { public get value(): T { return undefined as any; } private set value(next: T) {} }
            class Middle<U> extends Base<U> {}
            class Derived extends Middle<number> {}
            new Derived().value = 2;
            """, "TS2341", 4];

        yield return ["expression-private-setter-write", """
            const C = class Named { public get value(): number { return 7; } private set value(next: number) {} };
            new C().value = 2;
            """, "TS2341", 2];

        yield return ["same-name-distinct-setter-owner", """
            const A = class Same { public get value(): number { return 7; } private set value(next: number) {} };
            const B = class Same { write(other: InstanceType<typeof A>): void { other.value = 2; } };
            """, "TS2341", 2];

        yield return ["static-ordinary-private-getter-first", """
            class C {
                public static get value(): number { return 7; }
                private static set value(next: number) {}
            }
            C.value = 2;
            """, "TS2341", 5];

        yield return ["static-ordinary-private-setter-first", """
            class C {
                private static set value(next: number) {}
                public static get value(): number { return 7; }
            }
            C.value = 2;
            """, "TS2341", 5];

        yield return ["static-generic-private-getter-first", """
            class C<T> {
                public static get value(): number { return 7; }
                private static set value(next: number) {}
            }
            C.value = 2;
            """, "TS2341", 5];

        yield return ["static-generic-private-setter-first", """
            class C<T> {
                private static set value(next: number) {}
                public static get value(): number { return 7; }
            }
            C.value = 2;
            """, "TS2341", 5];

        yield return ["static-expression-private-getter-first", """
            const C = class Named {
                public static get value(): number { return 7; }
                private static set value(next: number) {}
            };
            C.value = 2;
            """, "TS2341", 5];

        yield return ["static-expression-private-setter-first", """
            const C = class Named {
                private static set value(next: number) {}
                public static get value(): number { return 7; }
            };
            C.value = 2;
            """, "TS2341", 5];

        yield return ["static-protected-getter-first", """
            class C {
                public static get value(): number { return 7; }
                protected static set value(next: number) {}
            }
            C.value = 2;
            """, "TS2445", 5];

        yield return ["static-protected-setter-first", """
            class C {
                protected static set value(next: number) {}
                public static get value(): number { return 7; }
            }
            C.value = 2;
            """, "TS2445", 5];

        yield return ["protected-base-read-after-alias-write", """
            class Base {
                protected get value(): number | undefined { return undefined; }
                protected set value(next: number | undefined) {}
            }
            class Derived extends Base {
                static read(original: Base): any {
                    const alias: Derived = original;
                    alias.value = 7;
                    return original.value;
                }
            }
            """, "TS2446", 9];
    }

    [Theory, MemberData(nameof(RejectedCases))]
    public void AccessorVisibilityUsesTheSelectedFacetAndItsDeclaringOwner(
        string name, string source, string expectedCode, int expectedLine)
    {
        Assert.NotEmpty(name);
        var diagnostic = Assert.Single(Errors(new TypeChecker(strictNullChecks: true, maxErrors: 20), source));
        Assert.Equal(expectedCode, diagnostic.TsCode);
        Assert.Equal(expectedLine, diagnostic.Line);
    }

    public static IEnumerable<object[]> AcceptedCases()
    {
        yield return ["same-owner-private-and-lexical-arrow", """
            class C {
                private get value(): number { return 7; }
                private set value(next: number) {}
                read(other: C): number { const arrow = () => { this.value = 2; return other.value; }; return arrow(); }
            }
            class Box<T> {
                private get value(): T { return undefined as any; }
                private set value(next: T) {}
                read(other: Box<T>, next: T): T { const arrow = () => { this.value = next; return other.value; }; return arrow(); }
            }
            """];

        yield return ["protected-permitted-instance-and-static-receivers", """
            class Base { protected get value(): number { return 7; } protected set value(next: number) {} }
            class Derived extends Base { read(other: Derived): number { this.value = 2; other.value = 3; return this.value + other.value; } }
            class StaticBase { protected static get value(): number { return 7; } protected static set value(next: number) {} }
            class StaticSibling extends StaticBase {}
            class StaticDerived extends StaticBase { static read(): number { StaticBase.value = 2; StaticSibling.value = 3; return StaticBase.value + StaticSibling.value; } }
            """];

        yield return ["nearest-public-accessor-masks", """
            class Base { get p(): any { return 1; } }
            class Child extends Base { set p(value: string) {} }
            const result: string = new Child().p;
            class Far { protected get value(): number { return 7; } }
            class Near extends Far { public get value(): number { return 9; } }
            const near: number = new Near().value;
            """];

        yield return ["protected-derived-read-after-alias-write", """
            class Base {
                protected get value(): number | undefined { return undefined; }
                protected set value(next: number | undefined) {}
            }
            class Derived extends Base {
                static read(original: Derived): number {
                    const alias: Derived = original;
                    alias.value = 7;
                    const narrowed: number = alias.value;
                    const readable: number | undefined = original.value;
                    return narrowed;
                }
            }
            """];

        yield return ["mixed-accessor-structural-compatibility", """
            class A0 {
                public get value(): number { return 1; }
                private set value(next: number) {}
            }
            class B0 {
                public get value(): number { return 1; }
                private set value(next: number) {}
            }
            const result0: A0 = new B0();

            class A1 {
                private set value(next: number) {}
                public get value(): number { return 1; }
            }
            class B1 {
                private set value(next: number) {}
                public get value(): number { return 1; }
            }
            const result1: A1 = new B1();

            class A2 {
                public static get value(): number { return 1; }
                private static set value(next: number) {}
            }
            class B2 {
                public static get value(): number { return 1; }
                private static set value(next: number) {}
            }
            const result2: typeof A2 = B2;

            class A3 {
                private static set value(next: number) {}
                public static get value(): number { return 1; }
            }
            class B3 {
                private static set value(next: number) {}
                public static get value(): number { return 1; }
            }
            const result3: typeof A3 = B3;
            """];
    }

    [Theory, MemberData(nameof(AcceptedCases))]
    public void PermittedReceiversAndPublicReadFacetsRemainAssignable(string name, string source)
    {
        Assert.NotEmpty(name);
        Assert.Empty(Errors(new TypeChecker(strictNullChecks: true, maxErrors: 20), source));
    }

    [Fact]
    public void ReusedCheckerRetainsAccessorFacetsOnPreviouslyDeclaredClasses()
    {
        var checker = new TypeChecker(strictNullChecks: true, maxErrors: 20);
        const string instanceDeclaration = "class RetainedInstance { public get value(): number { return 1; } private set value(next: number) {} change(next: number): void { this.value = next; } }";
        checker.Check(Parse(instanceDeclaration));
        checker.Check(Parse(""));
        var instanceWrite = Assert.Single(Errors(checker, "\nnew RetainedInstance().value = 2;"));
        Assert.Equal("TS2341", instanceWrite.TsCode);
        Assert.Equal(2, instanceWrite.Line);
        Assert.Empty(Errors(checker, "\nconst instanceRead: number = new RetainedInstance().value;"));

        // Check/Recovery preserve REPL declarations. Accessor facets must remain on
        // their owners after a later input, without redeclaring those class names.
        const string genericDeclaration = "class RetainedGeneric<T> { public get value(): T { return undefined as any; } private set value(next: T) {} change(next: T): void { this.value = next; } }";
        Assert.Empty(Errors(checker, genericDeclaration));
        checker.CheckWithRecovery(Parse(""));
        var genericWrite = Assert.Single(Errors(checker, "\nnew RetainedGeneric<number>().value = 2;"));
        Assert.Equal("TS2341", genericWrite.TsCode);
        Assert.Equal(2, genericWrite.Line);
        Assert.Empty(Errors(checker, "\nconst genericRead: number = new RetainedGeneric<number>().value;"));

        string directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "sharpts-accessor-visibility"));
        var resolver = new ModuleResolver(directory);
        const string staticDeclaration = "class RetainedStatic { protected static set value(next: number) {} public static get value(): number { return 3; } static change(next: number): void { this.value = next; } }";
        checker.Check(Parse(staticDeclaration));
        // A module input has its own child scope; preserve a prior REPL class
        // across the module check rather than expecting a module class to escape it.
        checker.CheckModules([new ParsedModule(Path.Combine(directory, "main.ts"), Parse("")) { IsScript = true }], resolver);
        Assert.Empty(checker.GetDiagnostics());
        var staticWrite = Assert.Single(Errors(checker, "\nRetainedStatic.value = 2;"));
        Assert.Equal("TS2445", staticWrite.TsCode);
        Assert.Equal(2, staticWrite.Line);
        Assert.Empty(Errors(checker, "\nconst staticRead: number = RetainedStatic.value;"));
    }

    private static Diagnostic[] Errors(TypeChecker checker, string source) => checker.CheckWithRecovery(Parse(source))
        .Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();

    private static List<Stmt> Parse(string source) => new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();
}
