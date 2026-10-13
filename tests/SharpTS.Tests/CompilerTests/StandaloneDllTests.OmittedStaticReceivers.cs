using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Fact]
    public void Isolated_Issue2016BorrowedApply_PreservesUndefinedAndExplicitReceivers()
    {
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", StaticMethodIdentityTests.BorrowedApplySource);
        var output = directory.GetPath("omitted-static-receivers.dll");
        var compile = IntegrationTests.CliTestHelper.RunCli(
            $"--no-tsconfig --compile \"{path}\" -o \"{output}\" --standalone --verify",
            directory.Path, TimeSpan.FromSeconds(60));
        Assert.True(compile.ExitCode == 0, compile.StandardOutput + compile.StandardError);
        Assert.Contains("IL verification passed.", compile.StandardOutput);
        Assert.DoesNotContain("SharpTS", GetAssemblyReferences(output));
        Assert.False(File.Exists(directory.GetPath("SharpTS.dll")));
        Assert.Equal(StaticMethodIdentityTests.BorrowedApplyExpected, ExecuteCompiledDllIsolated(output, timeoutMs: 30000,
            verifyStandardError: error => Assert.Empty(error), standardInput: ""));
    }
}
