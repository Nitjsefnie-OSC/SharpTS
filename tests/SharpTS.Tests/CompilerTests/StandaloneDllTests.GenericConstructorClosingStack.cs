using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Fact]
    public void Isolated_GenericConstructorClosingWithPendingStack_VerifyBeforeExecution()
    {
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var sourcePath = directory.CreateFile("main.ts", GeneratorTests.ClassExpressionMethodsLiveStorageSource);
        var output = directory.GetPath("generic-constructor-closing-stack.dll");
        var compile = IntegrationTests.CliTestHelper.RunCli(
            $"--no-tsconfig --compile \"{sourcePath}\" -o \"{output}\" --standalone --verify",
            directory.Path, TimeSpan.FromSeconds(60));

        Assert.True(compile.ExitCode == 0, compile.StandardOutput + compile.StandardError);
        Assert.Contains("IL verification passed.", compile.StandardOutput);
        Assert.All(GetAssemblyReferences(output), reference => Assert.StartsWith("System.", reference));
        Assert.False(File.Exists(directory.GetPath("SharpTS.dll")));
        Assert.Equal("11,12,13,14\n", ExecuteCompiledDllIsolated(output, timeoutMs: 30000,
            verifyStandardError: error => Assert.Empty(error), standardInput: ""));
    }
}
