using SharpTS.Modules;
using SharpTS.Tests.SharedTests;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.Modules;

public sealed class ApparentFunctionInterfaceTests
{
    private const string Augmentation = """
        interface Function { known(value: number): string; }
        interface Callable { (value: number): number; }
        declare const fn: Callable;
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactArrayBindLoadsTheStandardLibraryBeforeChecking(bool useDefaultLibraries)
        => Assert.Empty(CheckLoaded(ArrayConstructorApparentFunctionTests.IssueSource, useDefaultLibraries).GetDiagnostics());

    public static IEnumerable<object[]> AcceptedCases()
    {
        yield return ["inferred-array-alias", ArrayConstructorApparentFunctionTests.AliasSource];
        yield return ["callable-constructable-and-inherited-shapes", """
            interface Callable { (value: number): number; }
            interface Factory { new (value: number): { value: number }; }
            interface InheritedCallable extends Callable {}
            interface InheritedFactory extends Factory {}
            declare const callable: Callable, factory: Factory;
            declare const inheritedCallable: InheritedCallable, inheritedFactory: InheritedFactory;
            const bound = callable.bind(null, 1), applied = callable.apply(null, [1]);
            const called = inheritedCallable.call(null, 1), factoryBound = factory.bind(null, 1);
            const inheritedBound = inheritedFactory["bind"](null, 1);
            const callableLength: number = callable.length, inheritedLength: number = inheritedFactory["length"];
            """];
        yield return ["string-index-before-apparent-bracket-member", """
            interface IndexedCallable { (value: number): number; [key: string]: number; }
            declare const indexed: IndexedCallable;
            const indexedBind: number = indexed["bind"];
            const dottedBind: Function = indexed.bind;
            """];
        yield return ["generic-callable-interface", """
            interface GenericCallable<T> { (value: T): T; tag: T; }
            declare const generic: GenericCallable<number>;
            const bound = generic.bind(null, 1), indexedBound = generic["bind"](null, 2);
            const tag: number = generic.tag;
            """];
        yield return ["declared-and-inherited-shadow", """
            interface Own { (value: number): number; bind(value: number): string; }
            interface Child extends Own {}
            declare const own: Own, child: Child;
            const direct: string = own.bind(1), inherited: string = child["bind"](2);
            """];
        yield return ["global-augmentation-and-namespace-shadow", Augmentation + "\n" + """
            interface Factory { new (): {}; }
            declare const factory: Factory;
            const direct: string = fn.known(1), indexed: string = fn["known"](2);
            const constructable: string = factory.known(3);
            namespace Inner {
                interface Function { known(value: string): boolean; }
                const direct: string = fn.known(4), indexed: string = fn["known"](5);
            }
            """];
    }

    [Theory, MemberData(nameof(AcceptedCases))]
    public void CallableInterfaceShapesUseDeclaredGlobalFunctionMembers(string name, string source)
    {
        Assert.NotEmpty(name);
        Assert.Empty(CheckLoaded(source).GetDiagnostics());
    }

    public static IEnumerable<object[]> RejectedCases()
    {
        yield return ["noncallable-interface", """
            interface Plain { value: number; }
            declare const plain: Plain;
            plain.bind(null);
            """, "TS2339"];
        yield return ["unknown-callable-member", """
            interface Callable { (value: number): number; }
            declare const fn: Callable;
            fn.unrelated();
            """, "TS2339"];
        yield return ["own-shadow-argument", """
            interface Own { (value: number): number; bind(value: number): string; }
            declare const own: Own;
            own.bind("bad");
            """, "TS2345"];
        yield return ["inherited-shadow-result", """
            interface Own { (value: number): number; bind(value: number): string; }
            interface Child extends Own {}
            declare const child: Child;
            const result: number = child["bind"](1);
            """, "TS2322"];
        yield return ["declared-function-length", """
            interface Callable { (value: number): number; }
            declare const fn: Callable;
            const result: string = fn.length;
            """, "TS2322"];
        yield return ["string-index-does-not-inherit-apparent-callable-member", """
            interface IndexedCallable { (value: number): number; [key: string]: number; }
            declare const indexed: IndexedCallable;
            indexed["bind"]();
            """, "TS2349"];
        yield return ["augmentation-dot-argument", Augmentation + "\nfn.known(\"bad\");", "TS2345"];
        yield return ["augmentation-bracket-argument", Augmentation + "\nfn[\"known\"](\"bad\");", "TS2345"];
        yield return ["augmentation-dot-result", Augmentation + "\nconst result: number = fn.known(1);", "TS2322"];
        yield return ["augmentation-bracket-result", Augmentation + "\nconst result: number = fn[\"known\"](1);", "TS2322"];
        yield return ["namespace-shadow-preserves-global-signature", Augmentation + "\n" + """
            namespace Inner {
                interface Function { known(value: string): boolean; }
                fn.known("bad");
            }
            """, "TS2345"];
    }

    [Theory, MemberData(nameof(RejectedCases))]
    public void ApparentMembersPreserveDeclaredTypesAndRejectMissingMembers(string name, string source, string code)
    {
        Assert.NotEmpty(name);
        Assert.Contains(CheckLoaded(source).GetDiagnostics(), diagnostic => diagnostic.TsCode == code);
    }

    private static TypeChecker CheckLoaded(string source, bool useDefaultLibraries = false)
    {
        var path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "apparent-function-interface", "main.ts"));
        var options = useDefaultLibraries ? TypeScriptProgramOptions.Default
            : TypeScriptProgramOptions.Default with { Lib = ["lib.es5.d.ts"] };
        var resolver = new ModuleResolver(path, new Dictionary<string, string> { [path] = source }, options);
        var entry = resolver.LoadProgram(path);
        var modules = resolver.GetModulesInOrder(entry);
        Assert.Contains(modules, module => module.Path == "typescript-lib:lib.es5.d.ts");
        var checker = new TypeChecker(maxErrors: 50);
        checker.CheckModules(modules, resolver);
        return checker;
    }
}
