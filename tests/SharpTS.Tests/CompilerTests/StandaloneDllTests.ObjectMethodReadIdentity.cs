using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("original", false)]
    [InlineData("data-read-forms", true)]
    [InlineData("getter-and-tags", true)]
    [InlineData("async-member-tags", true)]
    [InlineData("constructor-values", true)]
    public void Saved_Issue2092ObjectMethodReads_VerifyBeforeExecution(string name, bool standalone)
    {
        var row = ObjectMethodReadIdentityTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        var output = directory.GetPath(name + ".dll");
        // The exact globalThis repro selects the existing soft runtime deployment.
        var deployment = standalone ? "--standalone" : "";
        var compile = IntegrationTests.CliTestHelper.RunCli(
            $"--no-tsconfig --compile \"{path}\" -o \"{output}\" {deployment} --verify",
            directory.Path, TimeSpan.FromSeconds(60));
        Assert.True(compile.ExitCode == 0, compile.StandardOutput + compile.StandardError);
        Assert.Contains("IL verification passed.", compile.StandardOutput);
        Assert.DoesNotContain("SharpTS", GetAssemblyReferences(output));
        Assert.Equal(!standalone, File.Exists(directory.GetPath("SharpTS.dll")));
        Assert.Equal((string)row[2], ExecuteCompiledDllIsolated(output, timeoutMs: 30000,
            verifyStandardError: error => Assert.Empty(error), standardInput: ""));
    }
}
