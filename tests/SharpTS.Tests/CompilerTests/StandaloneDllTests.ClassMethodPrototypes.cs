using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("issue")]
    [InlineData("methods")]
    [InlineData("accessors")]
    [InlineData("generators")]
    public void Isolated_Issue2020ClassMethods_VerifyPrototypeAndConstruction(string name)
    {
        var row = name switch
        {
            "generators" => new object[] { name, ClassMethodPrototypeTests.GeneratorControlsSource,
                string.Concat(Enumerable.Repeat("true object\ntrue\ntrue\ntrue\n", 8)) },
            "accessors" => new object[] { name, ClassMethodPrototypeTests.AccessorsSource,
                string.Concat(Enumerable.Repeat("false true\ntrue\ntrue\n", 4)) },
            _ => ClassMethodPrototypeTests.Cases().Single(row => (string)row[0] == name)
        };
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        var output = directory.GetPath("class-method-" + name + ".dll");
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
