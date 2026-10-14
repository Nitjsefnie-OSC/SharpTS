using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("issue")]
    [InlineData("expression")]
    [InlineData("nested")]
    [InlineData("methods")]
    public void Isolated_Issue2039ConstructorTypeOfIdentity(string name)
    {
        var row = ClassConstructorIdentityCompatibilityTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        AssertStandaloneConstructorIdentity(directory.Path, path,
            directory.GetPath("constructor-identity-" + name + ".dll"), (string)row[2]);
    }

    [Fact]
    public void Isolated_Issue2039ImportedConstructorTypeOfIdentity()
    {
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        foreach (var (file, source) in ClassConstructorIdentityCompatibilityTests.ModuleFiles())
            directory.CreateFile(file, source);
        AssertStandaloneConstructorIdentity(directory.Path, directory.GetPath("main.ts"),
            directory.GetPath("constructor-module.dll"), "true module\n");
    }

    private static void AssertStandaloneConstructorIdentity(
        string directory, string source, string output, string expected)
    {
        var compile = IntegrationTests.CliTestHelper.RunCli(
            $"--no-tsconfig --compile \"{source}\" -o \"{output}\" --standalone --verify",
            directory, TimeSpan.FromSeconds(60));
        Assert.True(compile.ExitCode == 0, compile.StandardOutput + compile.StandardError);
        Assert.Contains("IL verification passed.", compile.StandardOutput);
        Assert.DoesNotContain("SharpTS", GetAssemblyReferences(output));
        Assert.False(File.Exists(Path.Combine(directory, "SharpTS.dll")));
        Assert.Equal(expected, ExecuteCompiledDllIsolated(output, timeoutMs: 30000,
            verifyStandardError: error => Assert.Empty(error), standardInput: ""));
    }
}
