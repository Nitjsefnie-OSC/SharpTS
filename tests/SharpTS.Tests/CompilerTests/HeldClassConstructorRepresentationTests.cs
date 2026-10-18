using System.Reflection;
using System.Reflection.Emit;
using SharpTS.Compilation;
using SharpTS.Parsing;
using SharpTS.Tests.Infrastructure;
using SharpTS.TypeSystem;
using TSTypeInfo = SharpTS.TypeSystem.TypeInfo;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public sealed class HeldClassConstructorRepresentationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DocumentlessCheckedBindingsSurviveIndexClearingAndKeepUnrelatedSlotsNominal(bool recover)
    {
        const string source = """
            class Base { value = 1; }
            class Target extends Base { extra = 2; }
            class Source { value = 3; extra = 4; }
            const Held: typeof Target = Source;
            function construct(): Target { return new Held(); }
            function pass(value: Base): Base { return value; }
            class Unrelated { value = 8; }
            function nominal(value: Unrelated): Unrelated { return value; }
            """;
        var statements = new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();
        var checker = new TypeChecker();
        TypeMap map;
        if (recover)
        {
            var result = checker.CheckWithRecovery(statements);
            Assert.Empty(result.Diagnostics);
            map = result.TypeMap;
        }
        else map = checker.Check(statements);
        // Compile only from the published facts, after the checker's mutable index is gone.
        checker.Bindings.Clear();

        // Ordinary source annotations already have object public ABIs in some checker
        // paths. Pin the instance mapper policy directly, rather than requiring a new ABI.
        var policyAssembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("held_policy_" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.RunAndCollect);
        var policyModule = policyAssembly.DefineDynamicModule("policy");
        var names = new[] { "Base", "Target", "Source", "Unrelated" };
        var checkedClasses = names.ToDictionary(name => name, name => map.GetClassType(name)!);
        Assert.All(checkedClasses.Values, type => Assert.NotEqual(0, type.Core.DeclarationId));
        Assert.Equal(names.Length, checkedClasses.Values.Select(type => type.Core.DeclarationId).Distinct().Count());
        var builders = names.ToDictionary(name => name, name => policyModule.DefineType(name, TypeAttributes.Public));
        var mapper = new TypeMapper(policyModule);
        mapper.SetClassBuilders(builders);
        TSTypeInfo.Instance Instance(string name) => new(checkedClasses[name]);
        foreach (var name in names) Assert.Same(builders[name], mapper.MapTypeInfoStrict(Instance(name)));
        HeldClassConstructionInfo.Analyze(statements, map, mapper);
        Assert.Equal(typeof(object), mapper.MapTypeInfoStrict(Instance("Base")));
        Assert.Equal(typeof(object), mapper.MapTypeInfoStrict(Instance("Target")));
        Assert.Same(builders["Unrelated"], mapper.MapTypeInfoStrict(Instance("Unrelated")));
        Assert.Same(builders["Target"], mapper.MapTypeInfoStrict(checkedClasses["Target"]));

        var compiler = new ILCompiler("held_documentless_" + Guid.NewGuid().ToString("N"));
        compiler.Compile(statements, map);
        byte[] bytes = compiler.SaveToBytes();
        using var verifier = new ILVerifier(extraProbeDirectories: [AppContext.BaseDirectory]);
        using var stream = new MemoryStream(bytes);
        Assert.Empty(verifier.Verify(stream));
        var assembly = Assembly.Load(bytes);
        var entry = assembly.EntryPoint!;
        entry.Invoke(null, entry.GetParameters().Length == 0 ? null : [Array.Empty<string>()]);
        MethodInfo Function(string name) => assembly.GetType("$Program")!.GetMethod(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
        var construct = Function("construct");
        var pass = Function("pass");
        Assert.Equal(typeof(object), construct.ReturnType);
        Assert.Equal(typeof(object), pass.ReturnType);
        Assert.Equal(typeof(object), Assert.Single(pass.GetParameters()).ParameterType);
        object instance = construct.Invoke(null, null)!;
        Assert.Equal("Source", instance.GetType().Name);
        Assert.Same(instance, pass.Invoke(null, [instance]));
    }

    [Fact]
    public void SuppliedGenericArgumentsBelongOnlyToTheSameRuntimeDeclaration()
    {
        const string source = """
            class Target<T> { constructor(public value: T) {} }
            class Source<T> { constructor(public value: T) {} }
            const Same: typeof Target = Target;
            const Different: typeof Target = Source;
            function same(): any { return new Same<number>(1); }
            function different(): any { return new Different<number>(2); }
            function owned<T>(value: T): any { return new Same<T>(value); }
            """;
        var statements = new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();
        var map = new TypeChecker().Check(statements);
        var compiler = new ILCompiler("held_generic_identity_" + Guid.NewGuid().ToString("N"));
        compiler.Compile(statements, map);
        byte[] bytes = compiler.SaveToBytes();
        using var verifier = new ILVerifier(extraProbeDirectories: [AppContext.BaseDirectory]);
        using var stream = new MemoryStream(bytes);
        Assert.Empty(verifier.Verify(stream));
        var assembly = Assembly.Load(bytes);
        var entry = assembly.EntryPoint!;
        entry.Invoke(null, entry.GetParameters().Length == 0 ? null : [Array.Empty<string>()]);
        MethodInfo Function(string name) => assembly.GetType("$Program")!.GetMethod(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
        object same = Function("same").Invoke(null, null)!;
        object different = Function("different").Invoke(null, null)!;
        object owned = Function("owned").MakeGenericMethod(typeof(string)).Invoke(null, ["ok"])!;
        Assert.Equal("Target", same.GetType().GetGenericTypeDefinition().Name);
        Assert.Equal(typeof(double), Assert.Single(same.GetType().GetGenericArguments()));
        Assert.Equal("Source", different.GetType().GetGenericTypeDefinition().Name);
        Assert.Equal(typeof(object), Assert.Single(different.GetType().GetGenericArguments()));
        Assert.Equal(typeof(string), Assert.Single(owned.GetType().GetGenericArguments()));
    }

    [Fact]
    public void LiftingNestedGeneratorsKeepsTheCheckedClassOwnerAndConstructionType()
    {
        const string source = """
            class Target {
                value = 1;
                read(): number { function* unused() { yield 0; } return this.value; }
            }
            class Source {
                value = 2;
                read(): number { function* unused() { yield 0; } return this.value; }
            }
            const Held: typeof Target = Source;
            function construct(): Target { return new Held(); }
            console.log(construct().read(), construct() instanceof Source, new Target().read());
            """;
        Assert.Equal("2 true 1\n", TestHarness.RunCompiled(source));
    }
}
