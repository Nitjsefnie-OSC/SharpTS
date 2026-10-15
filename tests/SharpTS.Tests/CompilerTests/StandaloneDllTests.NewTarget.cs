using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("bound", true)]
    [InlineData("reflect", true)]
    [InlineData("parameters", true)]
    [InlineData("arrows", true)]
    [InlineData("classes", true)]
    [InlineData("expressions", true)]
    [InlineData("proxy", false)] // Proxy deliberately requires the deployed runtime (#2178).
    [InlineData("boundaries", true)]
    [InlineData("generics", true)]
    [InlineData("computed", true)]
    public void Saved_Issue2082NewTarget_VerifyBeforeExecution(string name, bool standalone)
    {
        var row = NewTargetTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        var output = directory.GetPath("new-target-" + name + ".dll");
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
