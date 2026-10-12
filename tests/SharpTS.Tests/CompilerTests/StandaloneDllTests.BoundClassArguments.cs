using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Fact]
    public void Isolated_Issue2011BoundClass_ShapesOptionalDefaultAndRestArguments()
    {
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", BoundClassArgumentTests.OptionalDefaultRestSource);
        var output = directory.GetPath("bound-class-arguments.dll");
        var compile = IntegrationTests.CliTestHelper.RunCli(
            $"--no-tsconfig --compile \"{path}\" -o \"{output}\" --standalone --verify",
            directory.Path, TimeSpan.FromSeconds(60));
        Assert.True(compile.ExitCode == 0, compile.StandardOutput + compile.StandardError);
        Assert.Contains("IL verification passed.", compile.StandardOutput);
        Assert.DoesNotContain("SharpTS", GetAssemblyReferences(output));
        Assert.False(File.Exists(directory.GetPath("SharpTS.dll")));
        Assert.Equal("44\ntrue\n1,2,3\n", ExecuteCompiledDllIsolated(output, timeoutMs: 30000,
            verifyStandardError: error => Assert.Empty(error), standardInput: ""));
    }

    [Fact]
    public void Isolated_Issue2011BoundClass_PreservesBoxedClrIntegerWidening()
    {
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", BoundClassArgumentTests.BoxedIntegerSource);
        var output = directory.GetPath("bound-class-integer.dll");
        var compile = IntegrationTests.CliTestHelper.RunCli(
            $"--no-tsconfig --experimentalDecorators --compile \"{path}\" -o \"{output}\" --standalone --verify",
            directory.Path, TimeSpan.FromSeconds(60));
        Assert.True(compile.ExitCode == 0, compile.StandardOutput + compile.StandardError);
        Assert.Contains("IL verification passed.", compile.StandardOutput);
        Assert.DoesNotContain("SharpTS", GetAssemblyReferences(output));
        Assert.Equal("7\n", ExecuteCompiledDllIsolated(output, timeoutMs: 30000,
            verifyStandardError: error => Assert.Empty(error), standardInput: ""));
    }
}
