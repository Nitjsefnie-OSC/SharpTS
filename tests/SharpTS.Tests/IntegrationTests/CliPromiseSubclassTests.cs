using Xunit;

namespace SharpTS.Tests.IntegrationTests;

public sealed class CliPromiseSubclassTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", """
            class P extends Promise<number> {}
            console.log(typeof P);
            """, "function\n"];

        yield return ["expression", """
            const P = class extends Promise<number> {}; console.log(typeof P);
            """, "function\n"];

        yield return ["generic-declaration", """
            class P<T> extends Promise<T> {}; console.log(typeof P);
            """, "function\n"];

        yield return ["generic-expression", """
            const P = class Named<T> extends Promise<T> {}; console.log(typeof P);
            """, "function\n"];
    }

    public static IEnumerable<object[]> LibraryModes()
    {
        foreach (var row in Cases())
        {
            yield return [row[0], row[1], row[2], false];
            if ((string)row[0] == "issue")
                yield return ["issue-noLib", row[1], row[2], true];
        }
    }

    [Theory, MemberData(nameof(LibraryModes))]
    public void LoadedLibrariesPreserveGenericPromiseHeritage(
        string name, string source, string expected, bool noLib)
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