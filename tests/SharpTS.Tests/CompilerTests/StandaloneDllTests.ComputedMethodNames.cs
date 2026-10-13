using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("issue")]
    [InlineData("kinds")]
    [InlineData("evaluation")]
    [InlineData("mutation")]
    public void Isolated_Issue2025ComputedMethodNames_VerifyEvaluatedKeys(string name)
    {
        var row = ClassComputedMethodNameTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var source = directory.CreateFile("main.ts", (string)row[1]);
        var output = directory.GetPath("computed-method-names-" + name + ".dll");
        var compile = IntegrationTests.CliTestHelper.RunCli(
            $"--no-tsconfig --compile \"{source}\" -o \"{output}\" --standalone --verify",
            directory.Path, TimeSpan.FromSeconds(60));
        Assert.True(compile.ExitCode == 0, compile.StandardOutput + compile.StandardError);
        Assert.Contains("IL verification passed.", compile.StandardOutput);
        Assert.DoesNotContain("SharpTS", GetAssemblyReferences(output));
        Assert.False(File.Exists(directory.GetPath("SharpTS.dll")));
        Assert.Equal((string)row[2], ExecuteCompiledDllIsolated(output, timeoutMs: 30000,
            verifyStandardError: error => Assert.Empty(error), standardInput: ""));
    }
}
