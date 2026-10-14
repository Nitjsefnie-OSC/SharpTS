using SharpTS.Tests.Infrastructure;
using SharpTS.TypeSystem;
using SharpTS.TypeSystem.Exceptions;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassConstructorIdentityCompatibilityTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", """
            class C {}
            function read(value: typeof C) { return value; }
            console.log(read(C) === C);
            """, "true\n"];

        yield return ["aliases", """
            class C { static tag = "C"; }
            function read(value: typeof C): typeof C { return value; }
            let Alias: typeof C = C;
            Alias = Alias;
            Alias = read(C);
            console.log(read(Alias) === C, Alias === C, Alias.tag);
            """, "true true C\n"];

        yield return ["expression", """
            const Owner = class Named { static tag = "named"; };
            function read(value: typeof Owner): typeof Owner { return value; }
            const Alias: typeof Owner = read(Owner);
            console.log(Alias === Owner, Alias.tag);
            """, "true named\n"];

        yield return ["nested", """
            function make() {
                class Local {}
                function read(value: typeof Local): typeof Local { return value; }
                const Alias: typeof Local = read(Local);
                console.log(Alias === Local);
                return Alias;
            }
            console.log(typeof make());
            """, "true\nfunction\n"];

        yield return ["methods", """
            class C {}
            class Reader {
                constructor(value: typeof C) { console.log(value === C); }
                read(value: typeof C): typeof C { return value; }
                static read(value: typeof C): typeof C { return value; }
            }
            const reader = new Reader(C);
            const arrow = (value: typeof C): typeof C => value;
            console.log(reader.read(C) === C, Reader.read(C) === C, arrow(C) === C);
            """, "true\ntrue true true\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void ConstructorsAreCompatibleWithTheirOwnTypeOf(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory]
    [InlineData("private value = 1;")]
    [InlineData("protected value = 1;")]
    [InlineData("#value = 1;")]
    [InlineData("static private value = 1;")]
    public void DistinctSameNamedConstructorDeclarationsRemainRejected(string member)
        => Assert.ThrowsAny<TypeCheckException>(() => new TypeChecker().Check(TestHarness.ParseOrThrow($$"""
            const Left = class Same { {{member}} };
            const Right = class Same { {{member}} };
            let Alias: typeof Left = Left;
            Alias = Alias;
            Alias = Right;
            """)));

    public static Dictionary<string, string> ModuleFiles() => new()
    {
        ["owner.ts"] = """
            export class C { static tag = "module"; }
            export function read(value: typeof C): typeof C { return value; }
            """,
        ["main.ts"] = """
            import { C as Owner, read } from './owner';
            const Alias: typeof Owner = read(Owner);
            console.log(Alias === Owner, Alias.tag);
            """
    };

    [Theory, ModeData]
    public void ImportedConstructorsRetainTheirTypeOfIdentity(ExecutionMode mode)
        => Assert.Equal("true module\n", TestHarness.RunModules(ModuleFiles(), "main.ts", mode));

    [Fact]
    public void ImportedConstructorsVerifyInTheModulePipeline()
        => Assert.Empty(TestHarness.CompileModulesAndVerifyOnly(ModuleFiles(), "main.ts"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImportedSameNamedPrivateConstructorsRemainDistinct(bool reverse)
    {
        var files = new Dictionary<string, string>
        {
            ["left.ts"] = "export class C { private value = 1; }",
            ["right.ts"] = "export class C { private value = 1; }",
            ["main.ts"] = (reverse
                ? "import { C as Right } from './right'; import { C as Left } from './left';"
                : "import { C as Left } from './left'; import { C as Right } from './right';")
                + "let Alias: typeof Left = Left; Alias = Right;"
        };
        Assert.Throws<TypeCheckDiagnosticException>(() => TestHarness.CompileModulesAndVerifyOnly(files, "main.ts"));
    }
}
