using SharpTS.Parsing;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.TypeCheckerTests;

public sealed class OrdinaryConstructorNestedInstanceTests
{
    private static string Wrap(string declarations, string member)
    {
        string targetMember = member == "factory" ? "static make() { return new A(); }" : "nested = new A();";
        string sourceMember = member == "factory" ? "static make() { return new B(); }" : "nested = new B();";
        return declarations + "\nclass Target { " + targetMember + " }\nclass Source { " + sourceMember
            + " }\nconst Alias: typeof Target = Source;";
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (string member in new[] { "factory", "field" })
        {
            foreach (bool sameName in new[] { false, true })
            foreach (bool compatible in new[] { false, true })
            {
                string sourceName = sameName ? "Same" : "Other";
                string sourceValue = compatible ? "value: number = 2; extra = true;" : "value: string = 'bad';";
                yield return [$"{member}-public-shape-same-name-{sameName}-compatible-{compatible}", Wrap($$"""
                    const A = class Same { value: number = 1; };
                    const B = class {{sourceName}} { {{sourceValue}} };
                    """, member), compatible];
            }

            foreach (var (name, declarations, compatible) in new[]
            {
                ("missing-member", "const A = class Same { value: number = 1; }; const B = class Same {};", false),
                ("empty-target-extra-private-brand", "const A = class Same {}; const B = class Same { #brand = 1; value = 2; };", true),
                ("distinct-private-brands", "const A = class Same { #brand = 1; }; const B = class Same { #brand = 2; };", false),
                ("shared-private-brands", "class Base { #brand = 1; } const A = class Same extends Base {}; const B = class Same extends Base {};", true),
                ("distinct-private-origins", "const A = class Same { private value = 1; }; const B = class Same { private value = 2; };", false),
                ("shared-private-origins", "class Base { private value = 1; } const A = class Same extends Base {}; const B = class Same extends Base {};", true),
                ("distinct-protected-origins", "const A = class Same { protected value = 1; }; const B = class Same { protected value = 2; };", false),
                ("derived-public-protected-origin", "class Base { protected value: number = 1; } const A = class Same extends Base {}; const B = class Same extends Base { public value: number = 2; };", true),
                ("ignore-constructor-static-shape", "const A = class Same { static value: number = 1; }; const B = class Same { static value: string = 'bad'; };", true),
                ("ignore-constructor-parameters", "const A = class Same { constructor(value?: number) {} }; const B = class Same { constructor(value?: string) {} };", true),
                ("recursive-members", "const A = class Same { next(): Same { return this; } value: number = 1; }; const B = class Same { next(): Same { return this; } value: number = 2; };", true),
                ("recursive-leaf-mismatch", "const A = class Same { next(): Same { return this; } value: number = 1; }; const B = class Same { next(): Same { return this; } value: string = 'bad'; };", false),
                ("inherited-concrete-generic", "class Base<T> { value!: T; } const A = class Same extends Base<number> {}; const B = class Same extends Base<number> {};", true),
                ("inherited-concrete-generic-mismatch", "class Base<T> { value!: T; } const A = class Same extends Base<number> {}; const B = class Same extends Base<string> {};", false)
            })
                yield return [$"{member}-{name}", Wrap(declarations, member), compatible];
        }

        foreach (bool compatible in new[] { false, true })
        {
            string sourceType = compatible ? "number" : "string";
            yield return [$"direct-generic-instances-compatible-{compatible}", $$"""
                class Box<T> { value!: T; }
                class Target { static make() { return new Box<number>(); } }
                class Source { static make() { return new Box<{{sourceType}}>(); } }
                const Alias: typeof Target = Source;
                """, compatible];
            yield return [$"recursive-generic-instances-compatible-{compatible}", $$"""
                class Link<T> { next!: Link<T>; value!: T; }
                class Target { nested = new Link<number>(); }
                class Source { nested = new Link<{{sourceType}}>(); }
                const Alias: typeof Target = Source;
                """, compatible];
        }
    }

    [Theory, MemberData(nameof(Cases))]
    public void NestedInstancesPreserveTheirPublicStructureAndNominalOrigins(
        string name, string source, bool compatible)
    {
        Assert.NotEmpty(name);
        var statements = new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();
        var diagnostics = new TypeChecker(new TypeCheckerOptions { StrictFunctionTypes = true, MaxErrors = 50 })
            .CheckWithRecovery(statements).Diagnostics;
        if (compatible) Assert.Empty(diagnostics);
        else
        {
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("TS2322", diagnostic.TsCode);
            Assert.Equal(source.Count(character => character == '\n') + 1, diagnostic.Line);
        }
    }
}
