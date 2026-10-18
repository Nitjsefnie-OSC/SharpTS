using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassExpressionIndexSignatureTests
{
    [Theory, ModeData]
    public void AnonymousDictionarySignatureHasNoRuntimeMember(ExecutionMode mode)
    {
        const string source = """
            const Dictionary = class { [key: string]: number; };
            const dictionary = new Dictionary();
            dictionary["answer"] = 42;
            const answer: number = dictionary["answer"];
            console.log(typeof Dictionary, answer, Object.hasOwn(dictionary, "key"));
            """;
        Assert.Equal("function 42 false\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ConstructorAndInstanceIndicesStayIndependent(ExecutionMode mode)
    {
        const string source = """
            const Dictionary = class {
                static [key: symbol]: string;
                [key: number]: number;
                [key: symbol]: number;
            };
            const key: symbol = Symbol("key");
            const dictionary = new Dictionary();
            Dictionary[key] = "static symbol";
            dictionary[0] = 1;
            dictionary[key] = 2;
            const staticSymbol: string = Dictionary[key];
            const instanceNumber: number = dictionary[0];
            const instanceSymbol: number = dictionary[key];
            console.log(staticSymbol, instanceNumber, instanceSymbol);
            """;
        Assert.Equal("static symbol 1 2\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void GenericValueTypeResolvesForEachInstantiation(ExecutionMode mode)
    {
        const string source = """
            const Dictionary = class<T> { [key: string]: T; };
            const numbers = new Dictionary<number>();
            const strings = new Dictionary<string>();
            numbers["answer"] = 42;
            strings["answer"] = "forty two";
            const numeric: number = numbers["answer"];
            const textual: string = strings["answer"];
            console.log(numeric, textual);
            """;
        Assert.Equal("42 forty two\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ComputedMembersKeepTheirRuntimeBehaviorAlongsideAnIndex(ExecutionMode mode)
    {
        const string source = """
            const fieldKey = "field";
            const methodKey = "method";
            const accessorKey = "accessor";
            const Dictionary = class {
                [key: string]: any;
                [fieldKey]: number = 1;
                [methodKey](): number { return 2; }
                get [accessorKey](): number { return 3; }
            };
            const dictionary = new Dictionary();
            dictionary["extra"] = 4;
            console.log(dictionary[fieldKey], dictionary[methodKey](), dictionary[accessorKey], dictionary["extra"]);
            """;
        Assert.Equal("1 2 3 4\n", TestHarness.Run(source, mode));
    }

    public static IEnumerable<object[]> InvalidIndexedUses()
    {
        string[] sources =
        [
            "const C = class { [key: string]: number; }; const value = new C(); value['x'] = 'wrong';",
            "const C = class { [key: string]: number; }; const value = new C(); const wrong: string = value['x'];",
            "const C = class { [key: number]: string; }; const value = new C(); value[0] = 1;",
            "const C = class { [key: number]: string; }; const value = new C(); const wrong: number = value[0];",
            "const C = class { [key: symbol]: string; }; const value = new C(); const key: symbol = Symbol('key'); value[key] = 1;",
            "const C = class { [key: symbol]: string; }; const value = new C(); const key: symbol = Symbol('key'); const wrong: number = value[key];",
            "const C = class { static [key: number]: string; }; C[0] = 1;",
            "const C = class { static [key: symbol]: string; }; const key: symbol = Symbol('key'); const wrong: number = C[key];",
            "const C = class<T> { [key: string]: T; }; const value = new C<number>(); const wrong: string = value['x'];",
        ];
        foreach (string source in sources)
            foreach (ExecutionMode mode in new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled })
                yield return [source, mode];
    }

    [Theory, MemberData(nameof(InvalidIndexedUses))]
    public void IndexedReadsAndWritesEnforceRetainedValueTypes(string source, ExecutionMode mode)
    {
        var files = new Dictionary<string, string> { ["main.ts"] = source };
        var exception = Assert.Throws<TypeCheckDiagnosticException>(() =>
            TestHarness.RunModules(files, "main.ts", mode));
        Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.TsCode == "TS2322");
    }
}
