using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("bound", true)]
    [InlineData("descriptor", true)]
    [InlineData("ordinary", true)]
    [InlineData("hooks", true)]
    [InlineData("variants", true)]
    [InlineData("readonly", true)]
    [InlineData("proxy", false)]
    [InlineData("hook-writes", true)]
    public void Saved_Issue2084FunctionHasInstance_VerifyBeforeExecution(string name, bool standalone)
    {
        var row = FunctionHasInstanceIntrinsicTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        var output = directory.GetPath("has-instance.dll");
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
