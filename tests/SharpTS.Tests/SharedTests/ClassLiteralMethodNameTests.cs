using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassLiteralMethodNameTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", """
            class C { "two words"() { return 42; } }
            const object: any = new C();
            console.log(object["two words"]());
            """, "42\n"];
        yield return ["expression", """
            const C = class {
                'two words'() { return 42; }
                "escaped\u0020name"() { return 43; }
                static "static value"() { return 44; }
            };
            const object: any = new C(), constructor: any = C;
            console.log(object["two words"](), object["escaped name"](), constructor["static value"]());
            console.log(object["escaped name"].name);
            """, "42 43 44\nescaped name\n"];
        yield return ["variants", """
            class C {
                "async"() { return 41; }
                *"sequence value"() { yield 42; }
                async "async value"() { return 43; }
                async *"async sequence"() { yield 44; }
                static *"static sequence"() { yield 45; }
            }
            async function run() {
                const object: any = new C(), constructor: any = C;
                console.log(object["async"](), object["sequence value"]().next().value);
                console.log(await object["async value"](), (await object["async sequence"]().next()).value,
                    constructor["static sequence"]().next().value);
            }
            run();
            """, "41 42\n43 44 45\n"];
        yield return ["numeric", """
            class C { 42() { return 1; } static 1e2() { return 2; } }
            const D = class { 43() { return 3; } static 2e2() { return 4; } };
            const object: any = new C(), constructor: any = C, other: any = new D(), otherConstructor: any = D;
            console.log(object[42](), object["42"](), constructor["100"](), other[43](), otherConstructor["200"]());
            """, "1 1 2 3 4\n"];
        yield return ["abstract", """
            abstract class Base { abstract "two words"(): number; }
            class C extends Base { "two words"() { return 42; } }
            const object: any = new C();
            console.log(object["two words"]());
            """, "42\n"];
        yield return ["constructors", """
            class C { "constructor"(public value: number) {} }
            const D = class { "constr\u0075ctor"(public value: number) {} };
            class Base { base = 1; }
            class Derived extends Base {
                value: number;
                "constructor"(value: number) { super(); this.value = value; }
            }
            console.log(new C(42).value, new D(43).value);
            const derived = new Derived(44);
            console.log(derived.base, derived.value);
            """, "42 43\n1 44\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void LiteralClassMethodNamesUseTheirDecodedPropertyKeys(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory]
    [InlineData("class C { static async constructor() {} }")]
    [InlineData("class C { static async \"constructor\"() {} }")]
    [InlineData("const C = class { static async constructor() {} };")]
    [InlineData("const C = class { static async \"constructor\"() {} };")]
    public void StaticAsyncConstructorNamedMethodsRemainValidJavaScriptSyntax(string source)
    {
        // These are ordinary static methods in JavaScript; TypeScript applies
        // separate checker restrictions to constructor-named declarations.
        Assert.NotEmpty(TestHarness.Parse(source));
    }

    [Theory]
    [InlineData("class C { *\"constructor\"() {} }")]
    [InlineData("class C { async \"constructor\"() {} }")]
    [InlineData("abstract class C { abstract \"constructor\"(); }")]
    [InlineData("class Base {} class C extends Base { override \"constructor\"() { super(); } }")]
    public void QuotedConstructorsRetainConstructorRestrictions(string source)
    {
        var parser = new SharpTS.Parsing.Parser(new SharpTS.Parsing.Lexer(source).ScanTokens());
        var result = parser.Parse();
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics,
            diagnostic => diagnostic.Message.Contains("constructor", StringComparison.Ordinal));
    }
}
