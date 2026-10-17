using SharpTS.Diagnostics;
using SharpTS.Modules;
using SharpTS.Parsing;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.TypeCheckerTests;

public sealed class OrdinaryConstructorCompatibilityTests
{
    private static IReadOnlyList<Diagnostic> Diagnostics(string source)
    {
        var statements = new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();
        return new TypeChecker(new TypeCheckerOptions { StrictFunctionTypes = true, MaxErrors = 50 })
            .CheckWithRecovery(statements).Diagnostics;
    }

    public static IEnumerable<object[]> RejectedCases()
    {
        foreach (var (name, target, source) in new[] {
            ("missing-instance", "value: number = 1;", ""),
            ("wrong-instance", "value: number = 1;", "value: string = 'bad';"),
            ("method-result", "read(): number { return 1; }", "read(): string { return 'bad'; }"),
            ("missing-static", "static value: number = 1;", ""),
            ("wrong-static", "static value: number = 1;", "static value: string = 'bad';"),
            ("static-method-result", "static read(): number { return 1; }", "static read(): string { return 'bad'; }"),
            ("instance-index", "[key: string]: number;", "[key: string]: string;"),
            ("missing-string-index", "[key: string]: number;", ""),
            ("missing-number-index", "[key: number]: number;", ""),
            ("extra-required-argument", "constructor(value: number) {}", "constructor(value: number, extra: number) {}"),
            ("implicit-zero-argument", "", "constructor(value: number) {}"),
            ("incompatible-argument", "constructor(value: number) {}", "constructor(value: string) {}"),
            ("incompatible-rest", "constructor(...values: number[]) {}", "constructor(...values: string[]) {}"),
            ("private-source-constructor", "constructor() {}", "private constructor() {}"),
            ("protected-source-constructor", "constructor() {}", "protected constructor() {}"),
            ("protected-target-private-source", "protected constructor() {}", "private constructor() {}"),
            ("missing-private-instance", "private value: number = 1;", ""),
            ("public-private-collision", "value: number = 1;", "private value: number = 1;"),
            ("distinct-static-protected", "protected static value: number = 1;", "protected static value: number = 1;"),
            ("distinct-instance-private-method", "#read() {}", "#read() {}"),
            ("missing-instance-private-brand", "#value = 1;", "") })
        {
            // Class expression index syntax remains unsupported (#2338).
            bool declaration = name is "instance-index" or "missing-string-index" or "missing-number-index";
            yield return [name, declaration ? $$"""
                class Target { {{target}} }
                class Source { {{source}} }
                const Alias: typeof Target = Source;
                """ : $$"""
                const Target = class Same { {{target}} };
                const Source = class Same { {{source}} };
                const Alias: typeof Target = Source;
                """];
        }
        // Implemented constructors lose their overload surface during return publication (#2340).
        yield return ["unmatched-overload", """
            declare class Target { constructor(value: number); constructor(value: string); }
            declare class Source { constructor(value: number); }
            const Alias: typeof Target = Source;
            """];
        yield return ["abstract-source", """
            class Target { value: number = 1; }
            abstract class Source { value: number = 1; }
            const Alias: typeof Target = Source;
            """];
        yield return ["inherited-generic-constructor", """
            class Base<T> { constructor(value: T) {} }
            class Target extends Base<number> {}
            class Source { constructor(value: string) {} }
            const Alias: typeof Target = Source;
            """];
        yield return ["inherited-generic-construct-record-union", """
            class Base<T> { factory!: { new(): T } | null; }
            class Target extends Base<number> {}
            class Source { factory!: { new(): string } | null; }
            const Alias: typeof Target = Source;
            """];
        foreach (string modifier in new[] { "", "static " })
            yield return [(modifier.Length == 0 ? "instance" : "static") + "-function-field-variance", $$"""
                class Animal { animal: number = 1; }
                class Dog extends Animal { dog: number = 1; }
                class Target { {{modifier}}callback = (value: Animal): void => {}; }
                class Source { {{modifier}}callback = (value: Dog): void => {}; }
                const Alias: typeof Target = Source;
                """];
        foreach (bool expression in new[] { false, true })
        foreach (string modifier in new[] { "", "static " })
        {
            string target = $"{modifier}value: number | undefined = 1;";
            string source = $"{modifier}value?: number;";
            yield return [$"required-from-optional-{expression}-{modifier}", ConstructorPair(expression, target, source)];
            yield return [$"required-callable-from-optional-{expression}-{modifier}", ConstructorPair(expression,
                $"{modifier}read = (): number => 1;", $"{modifier}read?: () => number;")];
        }
        yield return ["required-from-inherited-generic-optional", """
            class Base<T> { value?: T; }
            class Target { value: number | undefined = 1; }
            class Source extends Base<number> {}
            const Alias: typeof Target = Source;
            """];
        yield return ["derived-required-overrides-optional", """
            class Base { value?: number; }
            class Target extends Base { value = 1; }
            class Source extends Base {}
            const Alias: typeof Target = Source;
            """];
        yield return ["required-from-inherited-generic-static-optional", """
            class Base<T> { static value?: number; }
            class Target { static value: number | undefined = 1; }
            class Source extends Base<number> {}
            const Alias: typeof Target = Source;
            """];
        foreach (bool expression in new[] { false, true })
            yield return [$"optional-instance-with-no-common-member-{expression}", ConstructorPair(expression,
                "value?: number;", "unrelated = 1;")];
        foreach (string access in new[] { "private", "protected" })
        {
            yield return [$"optional-{access}-without-common-member", ConstructorPair(false,
                $"{access} value?: number;", "unrelated = 1;")];
            yield return [$"optional-{access}-public-collision", ConstructorPair(false,
                $"{access} value?: number;", "value = 1;")];
        }
        foreach (string modifier in new[] { "", "static " })
            yield return [$"required-from-optional-ambient-method-{modifier}", $$"""
                declare class Target { {{modifier}}read(): number; }
                declare class Source { {{modifier}}read?(): number; }
                const Alias: typeof Target = Source;
                """];
    }

    [Theory, MemberData(nameof(RejectedCases))]
    public void ConstructorStructureStillRequiresEverySignatureMemberAndNominalOrigin(string name, string source)
    {
        Assert.NotEmpty(name);
        var diagnostic = Assert.Single(Diagnostics(source));
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        string expectedCode = name is "missing-instance" or "derived-required-overrides-optional"
            or "required-from-inherited-generic-optional"
            || name.StartsWith("optional-instance-with-no-common-member-", StringComparison.Ordinal)
                ? "TS2741" : "TS2322";
        Assert.Equal(expectedCode, diagnostic.TsCode);
        Assert.Equal(FinalAliasLine(source), diagnostic.Line);
    }

    public static IEnumerable<object[]> AcceptedCases()
    {
        yield return ["public-source-superset", """
            class Target { value: number = 1; read(): number { return this.value; } static tag: string = "target"; }
            class Source { value: number = 2; read(): number { return this.value; } static tag: string = "source";
                extra = true; private sourceOnly = 1; }
            const Alias: typeof Target = Source;
            """];
        yield return ["optional-default-rest-and-fewer-parameters", """
            class Target { constructor(value: number, ...rest: number[]) {} }
            class Source { constructor(value: number, count: number = 2) {} }
            class Short { constructor() {} }
            const Alias: typeof Target = Source;
            const ShortAlias: typeof Target = Short;
            """];
        yield return ["inherited-concrete-generic-signature", """
            class Base<T> { constructor(value: T) {} }
            class Target extends Base<number> {}
            class Source { constructor(value: number) {} }
            const Alias: typeof Target = Source;
            """];
        yield return ["numeric-index-from-string-index", """
            class Target { [key: number]: number; }
            class Source { [key: string]: number; }
            const Alias: typeof Target = Source;
            """];
        yield return ["inherited-generic-construct-record-union", """
            class Base<T> { factory!: { new(): T } | null; }
            class Target extends Base<number> {}
            class Source { factory!: { new(): number } | null; }
            const Alias: typeof Target = Source;
            """];
        yield return ["shared-private-protected-and-instance-brand-origins", """
            class Base { private value = 1; protected other = 2; #brand = 3; protected static shared = 4; }
            class Target extends Base {}
            class Source extends Base { #extra = 5; }
            const Alias: typeof Target = Source;
            """];
        yield return ["public-derived-override-of-protected-member", """
            class Base { protected value: number = 1; }
            class Source extends Base { public value: number = 2; }
            const Alias: typeof Base = Source;
            """];
        yield return ["directional-constructor-visibility", """
            class PrivateTarget { private constructor() {} }
            class ProtectedTarget { protected constructor() {} }
            class ProtectedSource { protected constructor() {} }
            class PublicSource { constructor() {} }
            const privateAlias: typeof PrivateTarget = PublicSource;
            const privateProtectedAlias: typeof PrivateTarget = ProtectedSource;
            const protectedAlias: typeof ProtectedTarget = PublicSource;
            const protectedProtectedAlias: typeof ProtectedTarget = ProtectedSource;
            """];
        yield return ["concrete-source-abstract-target", """
            abstract class Target { abstract read(): number; }
            class Source { read(): number { return 1; } }
            const Alias: typeof Target = Source;
            """];
        yield return ["strict-constructor-and-method-bivariance", """
            class Animal { animal: number = 1; }
            class Dog extends Animal { dog: number = 1; }
            class Target { constructor(value: Animal) {} accept(value: Animal): void {} static accept(value: Animal): void {} }
            class Source { constructor(value: Dog) {} accept(value: Dog): void {} static accept(value: Dog): void {} }
            const Alias: typeof Target = Source;
            """];
        yield return ["recursive-record-function-members", """
            const Left = class Same {
                static next(value: { read: () => typeof Same }): { read: () => typeof Same } { return value; }
            };
            const Right = class Same {
                static next(value: { read: () => typeof Same }): { read: () => typeof Same } { return value; }
            };
            const Alias: typeof Left = Right;
            """];
        foreach (bool expression in new[] { false, true })
        foreach (string modifier in new[] { "", "static " })
        {
            yield return [$"missing-optional-{expression}-{modifier}", ConstructorPair(expression,
                $"{modifier}value?: number;", "")];
            yield return [$"required-satisfies-optional-{expression}-{modifier}", ConstructorPair(expression,
                $"{modifier}value?: number;", $"{modifier}value = 1;")];
            yield return [$"missing-optional-callable-{expression}-{modifier}", ConstructorPair(expression,
                $"{modifier}read?: () => number;", "")];
        }
        yield return ["missing-inherited-generic-optional", """
            class Base<T> { value?: T; }
            class Target extends Base<number> {}
            class Source {}
            const Alias: typeof Target = Source;
            """];
        yield return ["required-satisfies-inherited-generic-optional", """
            class Base<T> { value?: T; }
            class Target extends Base<number> {}
            class Source { value = 1; }
            const Alias: typeof Target = Source;
            """];
        yield return ["missing-inherited-generic-static-optional", """
            class Base<T> { static value?: number; }
            class Target extends Base<number> {}
            class Source { static unrelated = 1; }
            const Alias: typeof Target = Source;
            """];
        foreach (string access in new[] { "private", "protected" })
            yield return [$"missing-optional-{access}", ConstructorPair(false,
                $"{access} value?: number;", "")];
        foreach (string modifier in new[] { "", "static " })
            yield return [$"missing-optional-ambient-method-{modifier}", $$"""
                declare class Target { {{modifier}}read?(): number; }
                declare class Source {}
                const Alias: typeof Target = Source;
                """];
    }

    [Theory, MemberData(nameof(AcceptedCases))]
    public void OrdinaryConstructorsUseStructuralPublicContractsAndDirectionalVisibility(string name, string source)
    {
        Assert.NotEmpty(name);
        Assert.Empty(Diagnostics(source));
    }

    private static string ConstructorPair(bool expression, string target, string source) => expression
        ? $"const Target = class TargetName {{ {target} }};\nconst Source = class SourceName {{ {source} }};\nconst Alias: typeof Target = Source;"
        : $"class Target {{ {target} }}\nclass Source {{ {source} }}\nconst Alias: typeof Target = Source;";

    private static int FinalAliasLine(string source) => Array.FindLastIndex(source.Split('\n'),
        line => line.TrimStart().StartsWith("const Alias:", StringComparison.Ordinal)) + 1;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OptionalSourceCannotSatisfyRequiredMemberWithoutStrictNullChecks(bool isStatic)
    {
        string modifier = isStatic ? "static " : "";
        string source = ConstructorPair(false, $"{modifier}value: number | undefined = 1;", $"{modifier}value?: number;");
        var statements = new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();
        var diagnostic = Assert.Single(new TypeChecker(strictNullChecks: false).CheckWithRecovery(statements).Diagnostics);
        Assert.Equal("TS2322", diagnostic.TsCode);
        Assert.Equal(FinalAliasLine(source), diagnostic.Line);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecursiveSuccessCannotCacheOverALaterLeafMismatch(bool reverse)
    {
        string first = reverse ? "const one: typeof Right = Left;\nconst two: typeof Left = Right;"
            : "const one: typeof Left = Right;\nconst two: typeof Right = Left;";
        string source = """
            const Left = class Same { static take(value: typeof Same) { return value; } static marker: number = 1; };
            const Right = class Same { static take(value: typeof Same) { return value; } static marker: string = "bad"; };
            """ + "\n" + first + "\nconst dependent: typeof Left.take = Right.take;";
        var diagnostics = Diagnostics(source);
        Assert.Equal(3, diagnostics.Count);
        Assert.All(diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal("TS2322", diagnostic.TsCode);
        });
        Assert.Equal(new[] { 3, 4, 5 }, diagnostics.Select(diagnostic => diagnostic.Line));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ModuleImportOrderKeepsDistinctSameNamedConstructorStructures(bool reverse)
    {
        string directory = Path.Combine(Path.GetTempPath(), "ordinary-constructors-" + Guid.NewGuid().ToString("N"));
        string entryPath = Path.Combine(directory, "main.ts");
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Path.Combine(directory, "left.ts")] = "export const Value = class Same { static #brand = 1; static take(value: typeof Same) { return value; } };",
            [Path.Combine(directory, "right.ts")] = "export const Value = class Same { static #brand = 2; static take(value: typeof Same) { return value; } };",
            [entryPath] = (reverse
                ? "import { Value as Right } from './right'; import { Value as Left } from './left';"
                : "import { Value as Left } from './left'; import { Value as Right } from './right';")
                + "const selected: typeof Left = Left.take(Right);"
        };
        var resolver = new ModuleResolver(entryPath, files, TypeScriptProgramOptions.Disabled);
        var entry = resolver.LoadModule(entryPath);
        var checker = new TypeChecker(new TypeCheckerOptions { StrictFunctionTypes = true, MaxErrors = 50 });
        checker.CheckModules(resolver.GetModulesInOrder(entry), resolver);
        Assert.Empty(checker.GetDiagnostics());
    }
}
