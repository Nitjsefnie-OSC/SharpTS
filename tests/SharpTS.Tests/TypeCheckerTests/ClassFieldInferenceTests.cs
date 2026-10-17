using SharpTS.Diagnostics;
using SharpTS.Modules;
using SharpTS.Parsing;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.TypeCheckerTests;

public sealed class ClassFieldInferenceTests
{
    public static IEnumerable<object[]> RejectedCases()
    {
        foreach (bool expression in new[] { false, true })
        foreach (var (facet, modifier, name) in new[] {
            ("public-instance", "", "value"), ("private-instance", "", "#value"),
            ("public-static", "static ", "value"), ("private-static", "static ", "#value") })
        {
            string header = expression ? "const C = class Inner {" : "class C {";
            yield return [(expression ? "expression-" : "declaration-") + facet, $$"""
                {{header}}
                    {{modifier}}{{name}} = 1;
                    {{modifier}}read(): void {
                        const text: string = this.{{name}};
                    }
                };
                """, "TS2322", 4];
        }
        yield return ["static-write-before-field", """
            class C {
                static write(): void { this.value = "bad"; }
                static value = 1;
            }
            """, "TS2322", 2];
        yield return ["expression-private-static-write", """
            const C = class Inner {
                static #value = 1;
                static write(): void { this.#value = "bad"; }
            };
            """, "TS2322", 3];
        yield return ["methodless-anonymous-external-write", """
            const C = class { static value = 1; };
            C.value = "bad";
            """, "TS2322", 2];
        yield return ["private-number-call", """
            class C {
                #value = 1;
                read(): void { this.#value(); }
            }
            """, "TS2349", 3];
        yield return ["expression-private-static-number-call", """
            const C = class Inner {
                static #value = 1;
                static read(): void { this.#value(); }
            };
            """, "TS2349", 3];
        yield return ["private-arrow-argument", """
            class C {
                #value = (value: number) => value;
                read(): void { this.#value("bad"); }
            }
            """, "TS2345", 3];
        yield return ["methodless-expression-arrow-argument", """
            const C = class Inner { value = (value: number) => value; };
            new C().value("bad");
            """, "TS2345", 2];
        yield return ["generic-declaration-argument", """
            class Box<T> { value = (value: T): T => value; }
            new Box<number>().value("bad");
            """, "TS2345", 2];
        yield return ["generic-expression-argument", """
            const Box = class<T> { value = (value: T): T => value; };
            new Box<number>().value("bad");
            """, "TS2345", 2];
        yield return ["initializer-callback-annotated-write", """
            class C {
                value: number = 1;
                write = () => { this.value = "bad"; };
            }
            """, "TS2322", 3];
        yield return ["expression-initializer-callback-annotated-write", """
            const C = class Inner {
                value: number = 1;
                write = () => { this.value = "bad"; };
            };
            """, "TS2322", 3];
        yield return ["initializer-inner-guard-assignment", """
            class C {
                field = (() => {
                    let value: string | number = "text";
                    if (typeof value === "string") {
                        value = 1;
                        const text: string = value;
                    }
                    return value;
                })();
            }
            """, "TS2322", 6];
    }

    private static IReadOnlyList<Diagnostic> Diagnostics(string source)
    {
        var parsed = new Parser(new Lexer(source).ScanTokens()).Parse();
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        return new TypeChecker(maxErrors: 50).CheckWithRecovery(parsed.Statements).Diagnostics;
    }

    [Theory, MemberData(nameof(RejectedCases))]
    public void InferredFieldsArePublishedBeforeMemberBodiesAndExternalUses(
        string name, string source, string code, int line)
    {
        Assert.NotEmpty(name);
        var diagnostic = Assert.Single(Diagnostics(source));
        Assert.Equal(code, diagnostic.TsCode);
        Assert.Equal(line, diagnostic.Line);
    }

    public static IEnumerable<object[]> AcceptedCases()
    {
        yield return ["explicit-any-annotations-and-widening", """
            class C {
                value = 1; text = "a"; flag = true;
                dynamic: any = 1; #private: any = 1; static dynamic: any = 1; static #staticPrivate: any = 1;
                annotated: number = 1; callback: (value: number) => number = value => value;
                writeAnnotated = (value: number) => { this.annotated = value; };
                uninitialized;
                write(): void {
                    this.value = 2; this.text = "b"; this.flag = false;
                    this.dynamic = "free"; this.#private = "free";
                    this.dynamic(); this.#private(); this.annotated = 2; this.callback(2);
                }
                static write(): void { this.dynamic = "free"; this.#staticPrivate = "free"; this.#staticPrivate(); }
            }
            """];
        yield return ["generic-callables", """
            class Box<T> { value = (value: T): T => value; }
            class NumberBox extends Box<number> {}
            const Expression = class<T> { value = (value: T): T => value; };
            const number: number = new NumberBox().value(1);
            const text: string = new Box<string>().value("a");
            const expressionNumber: number = new Expression<number>().value(2);
            const expressionText: string = new Expression<string>().value("b");
            """];
        yield return ["recursive-this-and-sibling-fields", """
            class Chain {
                value = 1;
                self = () => this;
                direct = this;
                earlier = this.value;
                later = () => this.last;
                last = 2;
                next: Chain | undefined;
                read(): number { return this.self().direct.value + this.earlier + this.later(); }
            }
            const value: number = new Chain().read();
            """];
        yield return ["instance-initializer-flow-isolation", """
            let value: string | number = "text";
            class C { field = (value = 1); }
            const text: string = value;
            """];
        yield return ["instance-initializer-outer-guard-isolation", """
            function inspect(value: string | number) {
                if (typeof value === "string") {
                    class C { field = (value = 1); }
                    const text: string = value;
                }
            }
            """];
        yield return ["static-this-super-and-named-self", """
            class Base { static seed = 3; }
            class C extends Base {
                static copy = this.seed;
                static parent = super.seed;
                static self = () => this;
            }
            const Expression = class Inner {
                static seed = 4;
                static copy = this.seed;
                static self = () => Inner;
                value = 2;
                self = () => this;
            };
            const value: number = C.copy + C.parent + Expression.copy;
            const instance: number = new Expression().self().value;
            const owner: typeof Expression = Expression.self();
            """];
    }

    [Theory, MemberData(nameof(AcceptedCases))]
    public void FieldInferencePreservesAnnotationsGenericIdentitySelfAndFlow(string name, string source)
    {
        Assert.NotEmpty(name);
        Assert.Empty(Diagnostics(source));
    }

    private const string LiteralProvenanceSources = """
        function choice(): "a" | "b" { return "a"; }
        function record(): { kind: "a" } { return { kind: "a" }; }
        function array(): ("a" | "b")[] { return ["a"]; }
        function identity<T>(value: T): T { return value; }
        const fresh = 1, freshAlias = fresh;
        const fixed: 1 = 1;
        const fixedAlias = fixed;
        const asserted = 1 as const, assertedAlias = asserted;
        const typedObject: { kind: "a" } = { kind: "a" };
        const typedArray: ("a" | "b")[] = ["a"];
        class SeedOwner { readonly seed = 1; readonly fixed: 1 = 1; }
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FreshLiteralsWidenWhileAssertionsAndDeclaredReturnTypesStayFixed(bool expression)
    {
        string header = expression ? "const C = class Inner {" : "class C {";
        Assert.Empty(Diagnostics(LiteralProvenanceSources + "\n" + header + """

                literal = 1 as 1;
                tag = choice();
                object = record();
                array = array();
                nested = { kind: "a" as "a", count: 1 };
                records = [record(), typedObject];
                spread = [...typedArray];
                exactArray = ["a" as "a"];
                tuple = [1, "a"] as const;
                readonly readonlyValue = 1;
                copied = this.readonlyValue;
                foreignCopy = new SeedOwner().seed;
                readonly readonlyArray = [1];
                static readonly staticSeed = 1;
                static staticCopy = this.staticSeed;
                fresh = freshAlias;
                fixed = fixedAlias;
                asserted = assertedAlias;
                mutable = 1;
                negative = -1;
                negativeBigInt = -1n;
                mutableArray = [1];
                mutableObject = { count: 1 };
                callback = () => 1;
                blockCallback = () => { return 1; };
                localCallback = () => { const local = 1; const alias = local; return alias; };
                objectCallback = () => ({ count: 1 });
                fixedParameter = (fresh: 1) => fresh;
                asyncCallback = async () => 1;
                identityCall = identity(1);
                identityAlias = identity(freshAlias);
            };
            const value = new C();
            const literal: 1 = value.literal;
            const tag: "a" | "b" = value.tag;
            const kind: "a" = value.object.kind;
            const nested: "a" = value.nested.kind;
            const recordKind: "a" = value.records[0].kind;
            const exact: "a" = value.exactArray[0];
            const element: "a" | "b" = value.spread[0];
            const tuple: 1 = value.tuple[0];
            const readonlyValue: 1 = value.readonlyValue;
            const fixedRead: 1 = value.fixed;
            const assertedRead: 1 = value.asserted;
            const parameterResult: 1 = value.fixedParameter(1);
            value.fresh = 2;
            value.mutable = 2;
            value.negative = -2;
            value.negativeBigInt = -2n;
            value.copied = 2;
            value.foreignCopy = 2;
            C.staticCopy = 2;
            value.mutableArray.push(2);
            value.readonlyArray.push(2);
            value.mutableObject.count = 2;
            value.nested.count = 2;
            value.callback = () => 2;
            value.blockCallback = () => { return 2; };
            value.localCallback = () => 2;
            value.objectCallback = () => ({ count: 2 });
            value.asyncCallback = async () => 2;
            value.identityCall = 2;
            value.identityAlias = 2;
            """));
    }

    public static IEnumerable<object[]> LiteralProvenanceWrites()
    {
        foreach (bool expression in new[] { false, true })
        foreach (var (field, assignment, code) in new[]
        {
            ("field = 1 as 1;", "value.field = 2;", "TS2322"),
            ("field = choice();", "value.field = \"c\";", "TS2322"),
            ("field = record();", "value.field.kind = \"b\";", "TS2322"),
            ("field = { kind: \"a\" as \"a\" };", "value.field.kind = \"b\";", "TS2322"),
            ("field = [\"a\" as \"a\"];", "value.field.push(\"b\");", "TS2345"),
            ("field = [...typedArray];", "value.field.push(\"c\");", "TS2345"),
            ("field = [typedObject];", "value.field[0].kind = \"b\";", "TS2322"),
            ("field = fixedAlias;", "value.field = 2;", "TS2322"),
            ("field = assertedAlias;", "value.field = 2;", "TS2322"),
            ("readonly seed: 1 = 1; field = this.seed;", "value.field = 2;", "TS2322"),
            ("readonly seed = 1 as const; field = this.seed;", "value.field = 2;", "TS2322"),
            ("field = new SeedOwner().fixed;", "value.field = 2;", "TS2322"),
            ("field = (): 1 => 1;", "value.field = () => 2;", "TS2322"),
            ("field = () => 1 as 1;", "value.field = () => 2;", "TS2322"),
            ("field = () => { return 1 as 1; };", "value.field = () => 2;", "TS2322"),
            ("field = (fresh: 1) => fresh;", "value.field = (fresh: 1) => 2;", "TS2322"),
            ("field = identity(1 as 1);", "value.field = 2;", "TS2322"),
            ("field = async (): Promise<1> => 1;", "value.field = async () => 2;", "TS2322"),
            ("readonly field = 1;", "value.field = 2;", "TS2540"),
            ("field = { kind: \"a\" } as const;", "value.field.kind = \"b\";", "TS2540")
        })
            yield return [expression, field, assignment, code];
    }

    [Theory, MemberData(nameof(LiteralProvenanceWrites))]
    public void FixedLiteralFieldTypesRejectUnrelatedWrites(
        bool expression, string field, string assignment, string code)
    {
        string header = expression ? "const C = class Inner {" : "class C {";
        var diagnostic = Assert.Single(Diagnostics(LiteralProvenanceSources + "\n"
            + header + field + " };\nconst value = new C();\n" + assignment));
        Assert.Equal(code, diagnostic.TsCode);
    }

    [Fact]
    public void ImportedConstAliasesRetainLiteralWideningProvenance()
    {
        string directory = Path.Combine(Path.GetTempPath(), "sharpts-field-literals-" + Guid.NewGuid().ToString("N"));
        string entryPath = Path.Combine(directory, "main.ts");
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Path.Combine(directory, "values.ts")] = """
                export const fresh = 1;
                export const fixed: 1 = 1;
                export const asserted = 1 as const;
                """,
            [Path.Combine(directory, "aliases.ts")] = "export { fresh, fixed, asserted } from './values';",
            [entryPath] = """
                import { fresh as source, fixed, asserted } from "./aliases";
                const freshAlias = source, fixedAlias = fixed, assertedAlias = asserted;
                class C { value = freshAlias; fixed = fixedAlias; asserted = assertedAlias; }
                const value = new C();
                value.value = 2;
                const exact: 1 = value.fixed;
                const alsoExact: 1 = value.asserted;
                value.fixed = 2;
                value.asserted = 2;
                """
        };
        var resolver = new ModuleResolver(entryPath, files, TypeScriptProgramOptions.Disabled);
        var entry = resolver.LoadModule(entryPath);
        var checker = new TypeChecker(maxErrors: 50);
        checker.CheckModules(resolver.GetModulesInOrder(entry), resolver);
        Assert.Equal(new[] { "TS2322", "TS2322" }, checker.GetDiagnostics().Select(diagnostic => diagnostic.TsCode));
    }

    [Fact]
    public void NonStrictNullFieldsWidenAcrossScalarArrayAndObjectInitializers()
    {
        const string source = """
            class C { x = null; y = undefined; values = [null]; object = { v: null }; }
            const value = new C();
            value.x = 1;
            value.y = "text";
            value.values.push(1);
            value.object.v = 1;
            """;
        var statements = new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();
        Assert.Empty(new TypeChecker(strictNullChecks: false).CheckWithRecovery(statements).Diagnostics);
    }

    [Fact]
    public void StrictNullFieldInferenceDoesNotWidenNullToAny()
    {
        var diagnostic = Assert.Single(Diagnostics("class C { value = null; }\nnew C().value = 1;"));
        Assert.Equal("TS2322", diagnostic.TsCode);
        Assert.Equal(2, diagnostic.Line);
    }

    [Fact]
    public void ImportedDeclarationsAndExpressionsExposeTheirFinalFieldTypes()
    {
        string directory = Path.Combine(Path.GetTempPath(), "sharpts-field-inference-" + Guid.NewGuid().ToString("N"));
        string entryPath = Path.Combine(directory, "main.ts");
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Path.Combine(directory, "values.ts")] = """
                export class Model { value = 1; callback = (value: number) => value; }
                export const Expression = class Inner { static value = 1; };
                """,
            [entryPath] = """
                import { Model, Expression } from "./values";
                const invalid: string = new Model().value;
                new Model().callback("bad");
                Expression.value = "bad";
                """
        };
        var resolver = new ModuleResolver(entryPath, files, TypeScriptProgramOptions.Disabled);
        var entry = resolver.LoadModule(entryPath);
        var checker = new TypeChecker(maxErrors: 50);
        checker.CheckModules(resolver.GetModulesInOrder(entry), resolver);
        var diagnostics = checker.GetDiagnostics();
        Assert.Equal(new[] { ("TS2322", 2), ("TS2345", 3), ("TS2322", 4) },
            diagnostics.Select(diagnostic => (diagnostic.TsCode!, diagnostic.Line)));
        Assert.All(diagnostics, diagnostic => Assert.Equal(entryPath, diagnostic.FilePath, ignoreCase: true));
    }

    [Theory]
    [InlineData(false, "number", null)]
    [InlineData(false, "string", "TS2322")]
    [InlineData(true, "number", null)]
    [InlineData(true, "string", "TS2322")]
    public void LoadedLibraryFieldDependenciesUseTheCurrentClassReceiver(
        bool expression, string destination, string? code)
    {
        string header = expression ? "const C = class Inner {" : "class C {";
        string source = $$"""
            {{header}}
                value = 1;
                copy = this.value;
                read(): void {
                    const result: {{destination}} = this.copy;
                }
            };
            """;
        string path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "loaded-field-inference", "main.ts"));
        var options = TypeScriptProgramOptions.Default with { Lib = ["lib.es5.d.ts"] };
        var resolver = new ModuleResolver(path, new Dictionary<string, string> { [path] = source }, options);
        var entry = resolver.LoadProgram(path);
        var modules = resolver.GetModulesInOrder(entry);
        Assert.Contains(modules, module => module.Path == "typescript-lib:lib.es5.d.ts");
        var checker = new TypeChecker(maxErrors: 50);
        checker.CheckModules(modules, resolver);
        if (code == null) Assert.Empty(checker.GetDiagnostics());
        else
        {
            var diagnostic = Assert.Single(checker.GetDiagnostics());
            Assert.Equal(code, diagnostic.TsCode);
            Assert.Equal(5, diagnostic.Line);
        }
    }

    [Fact]
    public void LoadedLibraryContextualArrowsRetainScriptGlobalThis()
    {
        const string source = """
            let n = 1;
            class C {
                n: number = 2;
                explicit(this: C, value: number): number { return this.n + value; }
            }
            const c = new C();
            c.explicit = value => value + this.n;
            c.explicit = function(this: C, value: number) { return this.n + value; };
            """;
        string path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "loaded-field-inference", "global-this.ts"));
        var options = TypeScriptProgramOptions.Default with { Lib = ["lib.es5.d.ts"] };
        var resolver = new ModuleResolver(path, new Dictionary<string, string> { [path] = source }, options);
        var entry = resolver.LoadProgram(path);
        Assert.True(entry.IsScript);
        var modules = resolver.GetModulesInOrder(entry);
        Assert.Contains(modules, module => module.Path == "typescript-lib:lib.es5.d.ts");
        var checker = new TypeChecker(maxErrors: 50);
        checker.CheckModules(modules, resolver);
        var diagnostic = Assert.Single(checker.GetDiagnostics());
        Assert.Equal("TS2339", diagnostic.TsCode);
        Assert.Equal(7, diagnostic.Line);
    }
}
