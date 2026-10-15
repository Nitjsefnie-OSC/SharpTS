using SharpTS.Tests.Infrastructure;
using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Fact]
    public void Saved_Issue2091OriginalStaticSelf_VerifyBeforeExecution()
    {
        var bytes = TestHarness.CompileUncheckedAndVerify(ClassExpressionStaticSelfTests.OriginalSource);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var output = directory.GetPath("original-unchecked.dll");
        File.WriteAllBytes(output, bytes);
        directory.CreateFile("original-unchecked.runtimeconfig.json", """
            { "runtimeOptions": { "tfm": "net10.0", "framework": {
                "name": "Microsoft.NETCore.App", "version": "10.0.0"
            } } }
            """);
        Assert.DoesNotContain("SharpTS", GetAssemblyReferences(output));
        Assert.False(File.Exists(directory.GetPath("SharpTS.dll")));
        Assert.Equal("true\n", ExecuteCompiledDllIsolated(output, timeoutMs: 30000,
            verifyStandardError: error => Assert.Empty(error), standardInput: ""));
    }

    [Theory]
    [MemberData(nameof(ClassExpressionStaticSelfTests.Cases), MemberType = typeof(ClassExpressionStaticSelfTests))]
    public void Saved_Issue2091ClassExpressionStaticSelf_VerifyBeforeExecution(
        string name, string source, string expected)
    {
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", source);
        var output = directory.GetPath(name + ".dll");
        var compile = IntegrationTests.CliTestHelper.RunCli(
            $"--no-tsconfig --compile \"{path}\" -o \"{output}\" --standalone --verify",
            directory.Path, TimeSpan.FromSeconds(60));
        Assert.True(compile.ExitCode == 0, compile.StandardOutput + compile.StandardError);
        Assert.Contains("IL verification passed.", compile.StandardOutput);
        Assert.DoesNotContain("SharpTS", GetAssemblyReferences(output));
        Assert.False(File.Exists(directory.GetPath("SharpTS.dll")));
        Assert.Equal(expected, ExecuteCompiledDllIsolated(output, timeoutMs: 30000,
            verifyStandardError: error => Assert.Empty(error), standardInput: ""));
    }
}
