using SharpTS.Tests.IntegrationTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Isolated_Issue2198AwaitOperandException_VerifiesBeforeExecution(bool standalone)
    {
        const string source = """
            function fail() { throw new TypeError('boom'); }
            async function check() {
                try { await fail(); return 'missed'; }
                catch (error: any) {
                    return error instanceof TypeError ? 'caught' : 'wrong';
                }
            }
            check().then((value: any) => console.log(value))
                .catch((error: any) => console.log('TOP', error.name, error.message));
            """;
        using var directory = CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", source);
        var output = directory.GetPath("await-operand-exception.dll");
        var compile = CliTestHelper.RunCli(
            $"--no-tsconfig --noLib --compile \"{path}\" -o \"{output}\" --verify{(standalone ? " --standalone" : "")}",
            directory.Path, TimeSpan.FromSeconds(60));
        Assert.True(compile.ExitCode == 0, compile.StandardOutput + compile.StandardError);
        Assert.Contains("IL verification passed.", compile.StandardOutput);
        if (standalone)
        {
            Assert.DoesNotContain("SharpTS", GetAssemblyReferences(output));
            Assert.False(File.Exists(directory.GetPath("SharpTS.dll")));
        }

        var result = CaptureBoundedProcess(output, hidden: true);
        Assert.True(result.ExitCode == 0, result.Stderr);
        Assert.Equal("caught\n", result.Stdout);
        Assert.Empty(result.Stderr);
    }
}
