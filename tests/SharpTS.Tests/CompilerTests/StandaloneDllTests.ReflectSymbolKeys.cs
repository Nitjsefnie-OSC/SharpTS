using SharpTS.Tests.SharedTests;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("issue")]
    [InlineData("receiver-presence")]
    public void Isolated_Issue2159ReflectSymbolKeys_PreserveReceiverPresence(string name)
    {
        var row = ReflectSymbolKeyTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        AssertStandaloneConstructorIdentity(directory.Path, path,
            directory.GetPath("reflect-symbol-receiver-" + name + ".dll"), (string)row[2]);
    }

    [Theory]
    [InlineData("proxy")]
    [InlineData("proxy-receiver")]
    [InlineData("proxy-invariants")]
    public void Saved_Issue2159ReflectSymbolKeys_UseDeclaredProxyRuntime(string name)
    {
        var row = ReflectSymbolKeyTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        var output = directory.GetPath("reflect-symbol-proxy-" + name + ".dll");
        var compile = IntegrationTests.CliTestHelper.RunCli(
            $"--no-tsconfig --compile \"{path}\" -o \"{output}\" --standalone --verify",
            directory.Path, TimeSpan.FromSeconds(60));
        Assert.True(compile.ExitCode == 0, compile.StandardOutput + compile.StandardError);
        Assert.Contains("IL verification passed.", compile.StandardOutput);
        Assert.Contains("Note: output uses features needing the SharpTS runtime (Proxy)",
            compile.StandardOutput + compile.StandardError);
        Assert.DoesNotContain("SharpTS", GetAssemblyReferences(output));
        var runtime = directory.GetPath("SharpTS.dll");
        Assert.False(File.Exists(runtime));
        // Supply Proxy's declared optional runtime only after verifying the saved output.
        File.Copy(typeof(TypeChecker).Assembly.Location, runtime);
        Assert.Equal(File.ReadAllBytes(typeof(TypeChecker).Assembly.Location), File.ReadAllBytes(runtime));
        Assert.Equal((string)row[2], ExecuteCompiledDllIsolated(output, timeoutMs: 30000,
            verifyStandardError: error => Assert.Empty(error), standardInput: ""));
    }

    [Theory]
    [InlineData("data")]
    [InlineData("accessors")]
    [InlineData("receiver-data")]
    [InlineData("coercion")]
    [InlineData("coercion-errors")]
    [InlineData("class-methods")]
    [InlineData("coercion-order")]
    [InlineData("number-key-coercion")]
    public void Isolated_Issue2067ReflectSymbolKeys_VerifyBeforeExecution(string name)
    {
        var row = ReflectSymbolKeyTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        var output = directory.GetPath("reflect-symbol-" + name + ".dll");
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
