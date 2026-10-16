using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("instance-issue")]
    [InlineData("instance-packing")]
    [InlineData("instance-iterator-order")]
    [InlineData("instance-suspended-readers")]
    [InlineData("instance-pending-arguments")]
    public void Isolated_Issue2153InstancePrivateCalls_PackArguments(string name)
    {
        var row = PrivateCallSpreadTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        AssertStandaloneConstructorIdentity(directory.Path, path,
            directory.GetPath("instance-private-spreads-" + name + ".dll"), (string)row[2]);
    }

    [Theory]
    [InlineData("fields-methods-reference")]
    [InlineData("iterable-order-reference")]
    [InlineData("abrupt-reference")]
    [InlineData("awaited-reference")]
    [InlineData("generator-reference")]
    public void Isolated_Issue2064PrivateCallSpreads_UseGuestIterables(string name)
    {
        var row = PrivateCallSpreadTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        var output = directory.GetPath("private-spreads.dll");
        var compile = IntegrationTests.CliTestHelper.RunCli(
            $"--no-tsconfig --compile \"{path}\" -o \"{output}\" --standalone --verify",
            directory.Path, TimeSpan.FromSeconds(60));
        Assert.True(compile.ExitCode == 0, compile.StandardOutput + compile.StandardError);
        Assert.Contains("IL verification passed.", compile.StandardOutput);
        Assert.DoesNotContain("SharpTS", GetAssemblyReferences(output));
        Assert.False(File.Exists(directory.GetPath("SharpTS.dll")));
        Assert.Equal((string)row[2], ExecuteCompiledDllIsolated(output, timeoutMs: 30000,
            verifyStandardError: error => Assert.Empty(error), standardInput: ""));
    }
}
