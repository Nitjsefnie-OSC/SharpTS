using SharpTS.Tests.IntegrationTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Isolated_Issue2188AbortControllerValue_VerifiesAndRunsInBothDeployments(bool standalone)
    {
        const string source = """
            const Constructor: any = AbortController;
            console.log(typeof Constructor, Constructor === AbortController);
            const controller: any = new Constructor();
            const signal: any = controller.signal;
            console.log(signal.aborted);
            controller.abort("alias-stop");
            console.log(signal.aborted, signal.reason);
            """;
        using var directory = CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", source);
        var output = directory.GetPath("abort-controller-value.dll");
        var compile = CliTestHelper.RunCli(
            $"--no-tsconfig --compile \"{path}\" -o \"{output}\" --verify{(standalone ? " --standalone" : "")}",
            directory.Path, TimeSpan.FromSeconds(60));
        Assert.True(compile.ExitCode == 0, compile.StandardOutput + compile.StandardError);
        Assert.Contains("IL verification passed.", compile.StandardOutput);
        Assert.DoesNotContain("SharpTS", GetAssemblyReferences(output));
        Assert.False(File.Exists(directory.GetPath("SharpTS.dll")));

        var result = CaptureBoundedProcess(output, hidden: true);
        Assert.True(result.ExitCode == 0, result.Stderr);
        Assert.Equal("function true\nfalse\ntrue alias-stop\n", result.Stdout);
        Assert.Empty(result.Stderr);
    }
}
