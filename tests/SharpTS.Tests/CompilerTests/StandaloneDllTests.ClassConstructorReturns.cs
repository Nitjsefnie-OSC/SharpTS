using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("dynamic-methods")]
    [InlineData("direct")]
    [InlineData("expression")]
    [InlineData("objects")]
    [InlineData("primitives")]
    [InlineData("derived")]
    [InlineData("finally")]
    [InlineData("inherited")]
    [InlineData("suspended")]
    [InlineData("generic")]
    [InlineData("intrinsic")]
    public void Isolated_Issue2037ClassConstructorReturn_PreservesIdentity(string name)
    {
        var row = ClassConstructorReturnTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        var output = directory.GetPath("class-return-" + name + ".dll");
        // Intrinsic subclass cases use the same native built-in type definitions
        // as shared tests and existing native-subclass standalone coverage. Their
        // default declaration-library heritage resolution is a separate checker path.
        string library = name == "intrinsic" ? " --noLib" : "";
        var compile = IntegrationTests.CliTestHelper.RunCli(
            $"--no-tsconfig{library} --compile \"{path}\" -o \"{output}\" --standalone --verify",
            directory.Path, TimeSpan.FromSeconds(60));
        Assert.True(compile.ExitCode == 0, compile.StandardOutput + compile.StandardError);
        Assert.Contains("IL verification passed.", compile.StandardOutput);
        Assert.DoesNotContain("SharpTS", GetAssemblyReferences(output));
        Assert.False(File.Exists(directory.GetPath("SharpTS.dll")));
        Assert.Equal((string)row[2], ExecuteCompiledDllIsolated(output, timeoutMs: 30000,
            verifyStandardError: error => Assert.Empty(error), standardInput: ""));
    }
}
