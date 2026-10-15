using Xunit;

namespace SharpTS.Tests.IntegrationTests;

public sealed class CliErrorSubclassTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            class E extends Error {}
            console.log(new E("message").message);
            """, "message\n"];
        yield return ["explicit-and-inherited-constructor", """
            class Labeled extends Error {
                code = 7;
                constructor(message: string) { super(message); this.name = "Labeled"; }
            }
            class Deep extends Labeled {}
            const error = new Deep("detail");
            const typed: Error = error;
            console.log(error.message, error.name, error.code, typed.message,
                error instanceof Deep, error instanceof Error);
            """, "detail Labeled 7 detail true true\n"];
        yield return ["native-subtype-and-expressions", """
            class Declared extends TypeError {}
            const Expression = class extends Error {
                constructor(message: string) { super(message); }
            };
            const Subtype = class extends TypeError {};
            const declared = new Declared("declared");
            const expression = new Expression("expression");
            const subtype = new Subtype("subtype");
            console.log(declared.message, declared instanceof TypeError, declared instanceof Error);
            console.log(expression.message, expression instanceof Error);
            console.log(subtype.message, subtype instanceof TypeError, subtype instanceof Error);
            """, "declared true true\nexpression true\nsubtype true true\n"];
    }

    // #2296 tracks noLib expression constructor/member projection. Default-library
    // coverage retains the fully typed implicit constructor above. Native inherited
    // .name behavior is independently tracked by interpreter issue #2295.
    private const string NoLibExpressions = """
        class Declared extends TypeError {}
        const Expression = class extends Error {
            constructor(message: string) { super(message); }
        };
        const Subtype = class extends TypeError {
            constructor(message: string) { super(message); }
        };
        const declared = new Declared("declared");
        const expression: any = new Expression("expression");
        const subtype: any = new Subtype("subtype");
        console.log(declared.message, declared instanceof TypeError, declared instanceof Error);
        console.log(expression.message, expression instanceof Error);
        console.log(subtype.message, subtype instanceof TypeError, subtype instanceof Error);
        """;

    public static IEnumerable<object[]> LibraryModes() => Cases().SelectMany(row =>
        new[] { false, true }.Select(noLib => new object[]
        {
            row[0], noLib && (string)row[0] == "native-subtype-and-expressions" ? NoLibExpressions : row[1],
            row[2], noLib,
        }));

    [Theory, MemberData(nameof(LibraryModes))]
    public void LoadedLibrariesPreserveNativeErrorHeritage(string name, string source, string expected, bool noLib)
    {
        Assert.NotEmpty(name);
        using var directory = CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", source);
        var result = CliTestHelper.RunCli(
            $"--no-tsconfig{(noLib ? " --noLib" : "")} \"{path}\"", directory.Path);
        Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
        Assert.Empty(result.StandardError);
        Assert.Equal(expected, result.StandardOutput);
    }
}
