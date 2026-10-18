using SharpTS.Diagnostics;
using SharpTS.Modules;
using SharpTS.Parsing;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.TypeCheckerTests;

public sealed class AmbientClassInterfaceMergeTests
{
    private const string InterfaceDeclaration = """
        interface Inherited<P> { inherited: P; [index: number]: P; }
        interface Component<P = { label: string }, S = { ready: boolean }, SS = number> extends Inherited<P> {
            props: P;
            state: S;
            snapshot: SS;
        }
        """;

    private const string ClassDeclaration = """
        class Component<P, S> {
            constructor(props: P);
            own: number;
            describe(): string;
        }
        """;

    private const string ValidConsumer = """
        import Lib = require("fixture");
        class Defaulted extends Lib.Component {
            read(): string {
                const label: string = this.props.label;
                const ready: boolean = this.state.ready;
                const snapshot: number = this.snapshot;
                const inherited: { label: string } = this.inherited;
                const own: number = this.own;
                return this.describe();
            }
        }
        class OneArgument extends Lib.Component<{ count: number }> {
            read(): number {
                const ready: boolean = this.state.ready;
                const snapshot: number = this.snapshot;
                return this.inherited.count + this.props.count + this.own;
            }
        }
        class TwoArguments extends Lib.Component<{ count: number }, { done: string }> {
            read(): string {
                const count: number = this.props.count;
                const snapshot: number = this.snapshot;
                return this.state.done;
            }
        }
        const constructed = new Lib.Component<{ count: number }, { ready: boolean }>({ count: 1 });
        const own: number = constructed.own;
        const description: string = constructed.describe();
        """;

    [Fact]
    public void DeclarationRecoveryPublishesTheLaterClassAndConstructor()
    {
        const string library = """
            declare module "fixture" {
                export = Lib;
                namespace Lib {
                    type Broken = ReturnType<number>;
                    class Component { constructor(value: number); own: number; }
                }
            }
            """;
        var program = CheckProgram(library, """
            import Lib = require("fixture");
            class Derived extends Lib.Component {}
            const value = new Lib.Component(1);
            const own: number = value.own;
            """);

        // Dependency declarations retain the existing suppressed-diagnostic policy.
        AssertNoErrors(program.Checker);
        var owner = Assert.IsType<TypeInfo.Class>(ExportedNamespace(program, "Lib").GetMember("Component"));
        Assert.NotEqual(0, owner.Core.DeclarationId);
        Assert.IsType<TypeInfo.Function>(owner.Methods["constructor"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothDeclarationOrdersPreserveDefaultsMembersAndClassOwnerOnRecheck(bool classFirst)
    {
        var program = CheckProgram(Library(classFirst), ValidConsumer);

        AssertNoErrors(program.Checker);
        AssertMergedOwner(program);

        // Exercise the same parsed declarations through collection/completion again.
        foreach (var module in program.Modules)
            module.IsTypeChecked = false;
        program.TypeMap = program.Checker.CheckModules(program.Modules, program.Resolver);

        AssertNoErrors(program.Checker);
        AssertMergedOwner(program);
    }

    [Fact]
    public void MergedInheritedAndDefaultedMembersStillRejectWrongTypes()
    {
        var program = CheckProgram(Library(classFirst: false), """
            import Lib = require("fixture");
            const value = new Lib.Component<{ count: number }>({ count: 1 });
            const wrongProps: string = value.props.count;
            const wrongInherited: string = value.inherited.count;
            const wrongSnapshot: string = value.snapshot;
            """);

        var errors = program.Checker.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Equal(new[] { 3, 4, 5 }, errors.Select(d => d.Line));
        Assert.All(errors, error =>
        {
            Assert.Equal("TS2322", error.TsCode);
            Assert.Equal("main.ts", Path.GetFileName(error.FilePath));
            Assert.Contains("number", error.Message);
            Assert.Contains("string", error.Message);
        });
    }

    [Fact]
    public void SameSpelledClassesInOtherAmbientNamespacesRetainSeparateOwners()
    {
        string library = Library(classFirst: false) + "\n" + """
            declare module "other" {
                export = Other;
                namespace Other {
                    interface Component<P = { text: string }> { props: P; }
                    class Component<P> { constructor(props: P); own: boolean; }
                }
            }
            """;
        var program = CheckProgram(library, """
            import Lib = require("fixture");
            import Other = require("other");
            const first = new Lib.Component<{ count: number }, {}>({ count: 1 });
            const second = new Other.Component<{ text: string }>({ text: "ok" });
            const firstOwn: number = first.own;
            const secondOwn: boolean = second.own;
            const firstProps: number = first.props.count;
            const secondProps: string = second.props.text;
            const invalid: number = second.props.text;
            """);

        var error = Assert.Single(program.Checker.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal("TS2322", error.TsCode);
        Assert.Equal(9, error.Line);
        var first = Assert.IsType<TypeInfo.GenericClass>(ExportedNamespace(program, "Lib").GetMember("Component"));
        var second = Assert.IsType<TypeInfo.GenericClass>(ExportedNamespace(program, "Other").GetMember("Component"));
        Assert.NotEqual(0, first.Core.DeclarationId);
        Assert.NotEqual(0, second.Core.DeclarationId);
        Assert.NotEqual(first.Core.DeclarationId, second.Core.DeclarationId);
    }

    [Fact]
    public void DeclarationRecoveryDoesNotSuppressLaterOrdinarySourceDiagnostics()
    {
        var program = CheckProgram("""
            declare module "fixture" {
                export = Lib;
                namespace Lib { type Broken = ReturnType<number>; class Component {} }
            }
            """, "import Lib = require('fixture'); class Derived extends Lib.Component {}");
        AssertNoErrors(program.Checker);

        var statements = new Parser(new Lexer("""
            type Invalid = ReturnType<number>;
            const wrong: number = "bad";
            """).ScanTokens()).ParseOrThrow();
        var diagnostics = program.Checker.CheckWithRecovery(statements).Diagnostics;

        Assert.Equal(2, diagnostics.Count);
        Assert.Contains(diagnostics, d => d.TsCode == "TS2344" && d.Line == 1);
        Assert.Contains(diagnostics, d => d.TsCode == "TS2322" && d.Line == 2);
    }

    [Theory]
    [InlineData("any", "string", false)]
    [InlineData("number", "string", false)]
    [InlineData("any", "any", true)]
    [InlineData("FixtureText", "string", true)]
    public void MergedDefaultsRequireSemanticIdentity(string classDefault, string interfaceDefault, bool accepted)
    {
        string source = $$"""
            type FixtureText = string;
            declare class Component<T = {{classDefault}}> {}
            interface Component<T = {{interfaceDefault}}> {}
            """;
        var statements = new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();
        var result = new TypeChecker(strictNullChecks: true).CheckWithRecovery(statements);

        if (accepted)
            Assert.Empty(result.Diagnostics);
        else
            Assert.Equal("TS2428", Assert.Single(result.Diagnostics).TsCode);
    }

    [Fact]
    public void LaterDefaultedInterfaceParameterKeepsEarlierMembers()
    {
        var program = CheckProgram("""
            declare module "fixture" {
                export = Lib;
                namespace Lib {
                    interface Component<T = number> { first: T; }
                    class Component<T> { constructor(value: T); }
                    interface Component<T = number, U = string> { second: U; }
                }
            }
            """, """
            import Lib = require("fixture");
            const value = new Lib.Component(1);
            const first: number = value.first;
            const second: string = value.second;
            const wrong: number = value.second;
            """);

        var error = Assert.Single(program.Checker.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Equal("TS2322", error.TsCode);
        Assert.Equal(5, error.Line);
        var owner = Assert.IsType<TypeInfo.GenericClass>(ExportedNamespace(program, "Lib").GetMember("Component"));
        Assert.Equal(new[] { "T", "U" }, owner.TypeParams.Select(parameter => parameter.Name));
        Assert.Equal(new[] { "first", "second" }, owner.FieldTypes.Keys.Order());
    }

    private static string Library(bool classFirst) => $$"""
        declare module "fixture" {
            export = Lib;
            namespace Lib {
                {{(classFirst ? ClassDeclaration : InterfaceDeclaration)}}
                {{(classFirst ? InterfaceDeclaration : ClassDeclaration)}}
            }
        }
        """;

    private static void AssertMergedOwner(CheckedProgram program)
    {
        var exported = ExportedNamespace(program, "Lib");
        var value = Assert.IsType<TypeInfo.GenericClass>(exported.GetMember("Component"));
        var type = Assert.IsType<TypeInfo.GenericClass>(exported.Types["Component"]);
        Assert.NotEqual(0, value.Core.DeclarationId);
        Assert.Equal(value.Core.DeclarationId, type.Core.DeclarationId);
        Assert.Equal(new[] { "P", "S", "SS" }, value.TypeParams.Select(p => p.Name));
        Assert.All(value.TypeParams, parameter => Assert.NotNull(parameter.Default));
        Assert.IsType<TypeInfo.TypeParameter>(value.FieldTypes["props"]);
        Assert.IsType<TypeInfo.TypeParameter>(value.FieldTypes["inherited"]);
        Assert.Equal("P", Assert.IsType<TypeInfo.TypeParameter>(value.NumberIndexType).Name);
        Assert.Equal(TokenType.TYPE_NUMBER, Assert.IsType<TypeInfo.Primitive>(value.FieldTypes["own"]).Type);
        Assert.IsType<TypeInfo.String>(Assert.IsType<TypeInfo.Function>(value.Methods["describe"]).ReturnType);
        var constructor = Assert.IsType<TypeInfo.Function>(value.Methods["constructor"]);
        Assert.Equal("P", Assert.IsType<TypeInfo.TypeParameter>(Assert.Single(constructor.ParamTypes)).Name);

        var declaration = program.Modules.SelectMany(m => m.Statements).OfType<Stmt.Namespace>()
            .Single(ns => ns.Name.Lexeme == "Lib").Members.OfType<Stmt.Class>().Single();
        // Bare ambient namespace classes are stored in Types; their constructor value binding
        // still belongs to the exact class declaration, without requiring an explicit export.
        var valueBinding = Assert.IsType<BindingSymbol>(exported.GetValueBinding("Component"));
        Assert.Same(valueBinding, program.TypeMap.GetValueBinding(declaration.Name));
        var checkedClass = Assert.IsType<TypeInfo.Class>(program.TypeMap.GetClassType(declaration));
        Assert.Equal(value.Core.DeclarationId, checkedClass.Core.DeclarationId);
        Assert.Equal(value.NumberIndexType, checkedClass.NumberIndexType);
        foreach (string member in new[] { "props", "state", "snapshot", "inherited", "own" })
            Assert.Equal(value.FieldTypes[member], checkedClass.FieldTypes[member]);
        Assert.Equal(value.Methods["constructor"], checkedClass.Methods["constructor"]);
    }

    private static TypeInfo.Namespace ExportedNamespace(CheckedProgram program, string name) =>
        Assert.Single(program.Modules.Select(m => m.ExportAssignmentType)
            .OfType<TypeInfo.Namespace>(), ns => ns.Name == name);

    private static void AssertNoErrors(TypeChecker checker) =>
        Assert.DoesNotContain(checker.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);

    private static CheckedProgram CheckProgram(string library, string consumer)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"sharpts_ambient_merge_{Guid.NewGuid():N}");
        string libraryPath = Path.Combine(directory, "library.d.ts");
        string entryPath = Path.Combine(directory, "main.ts");
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [libraryPath] = library,
            [entryPath] = consumer,
        };
        var resolver = new ModuleResolver(entryPath, files,
            new TypeScriptProgramOptions { NoLib = true, PreferDeclarationFiles = true });
        var declaration = resolver.LoadModule(libraryPath);
        resolver.RegisterAmbientModuleDeclarations([declaration]);
        var entry = resolver.LoadProgram(entryPath);
        var modules = resolver.GetModulesInOrder(entry);
        if (!modules.Contains(declaration))
            modules.Add(declaration);
        resolver.RegisterAmbientModuleDeclarations(modules);
        modules = resolver.GetModulesInOrder(modules);
        var checker = new TypeChecker(strictNullChecks: true, maxErrors: 30);
        var typeMap = checker.CheckModules(modules, resolver);
        return new CheckedProgram(checker, resolver, modules, typeMap);
    }

    private sealed class CheckedProgram(
        TypeChecker checker, ModuleResolver resolver, List<ParsedModule> modules, TypeMap typeMap)
    {
        public TypeChecker Checker { get; } = checker;
        public ModuleResolver Resolver { get; } = resolver;
        public List<ParsedModule> Modules { get; } = modules;
        public TypeMap TypeMap { get; set; } = typeMap;
    }
}
