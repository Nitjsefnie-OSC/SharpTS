using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Isolated_Issue2012StaticStateMachines_PreserveReceivers(bool interleaved)
    {
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var source = interleaved ? StaticStateMachineReceiverTests.InterleavedSource
            : StaticStateMachineReceiverTests.IssueSource;
        var path = directory.CreateFile("main.ts", source);
        var output = directory.GetPath("static-receiver.dll");
        var compile = IntegrationTests.CliTestHelper.RunCli(
            $"--no-tsconfig --compile \"{path}\" -o \"{output}\" --standalone --verify",
            directory.Path, TimeSpan.FromSeconds(60));
        Assert.True(compile.ExitCode == 0, compile.StandardOutput + compile.StandardError);
        Assert.Contains("IL verification passed.", compile.StandardOutput);
        Assert.DoesNotContain("SharpTS", GetAssemblyReferences(output));
        Assert.False(File.Exists(directory.GetPath("SharpTS.dll")));
        var expected = interleaved ? StaticStateMachineReceiverTests.InterleavedOutput : "true\ntrue\ntrue\n";
        Assert.Equal(expected, ExecuteCompiledDllIsolated(output, timeoutMs: 30000,
            verifyStandardError: error => Assert.Empty(error), standardInput: ""));
    }
}
