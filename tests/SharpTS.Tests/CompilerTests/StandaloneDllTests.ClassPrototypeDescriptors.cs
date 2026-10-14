using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("issue")]
    [InlineData("definitions")]
    public void Isolated_Issue2019ClassPrototypes_PreserveIntrinsicDescriptors(string name)
    {
        var source = name == "issue"
            ? ClassPrototypeDescriptorTests.IssueSource
            : ClassPrototypeDescriptorTests.DefinitionSource;
        var expected = name == "issue"
            ? "false\ntrue\n"
            : ClassPrototypeDescriptorTests.DefinitionExpected;
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", source);
        var output = directory.GetPath("class-prototype-" + name + ".dll");
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

    [Fact]
    public void Isolated_Issue2058StrictConstructorPrototypeWrites_RespectDescriptors()
    {
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", ClassPrototypeDescriptorTests.StrictPrototypeWriteSource);
        var output = directory.GetPath("strict-class-prototypes.dll");
        var compile = IntegrationTests.CliTestHelper.RunCli(
            $"--no-tsconfig --compile \"{path}\" -o \"{output}\" --standalone --verify",
            directory.Path, TimeSpan.FromSeconds(60));
        Assert.True(compile.ExitCode == 0, compile.StandardOutput + compile.StandardError);
        Assert.Contains("IL verification passed.", compile.StandardOutput);
        Assert.DoesNotContain("SharpTS", GetAssemblyReferences(output));
        Assert.False(File.Exists(directory.GetPath("SharpTS.dll")));
        Assert.Equal(ClassPrototypeDescriptorTests.StrictPrototypeWriteExpected,
            ExecuteCompiledDllIsolated(output, timeoutMs: 30000,
                verifyStandardError: error => Assert.Empty(error), standardInput: ""));
    }
}
