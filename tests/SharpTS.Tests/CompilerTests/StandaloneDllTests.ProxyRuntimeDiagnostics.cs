using SharpTS.Tests.IntegrationTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("ordinary", true)]
    [InlineData("revocable", true)]
    [InlineData("ordinary", false)]
    [InlineData("revocable", false)]
    public void Isolated_Issue2178ProxyRuntimeDependencyHasActionableDiagnostic(string name, bool standalone)
    {
        string source = name == "ordinary" ? """
            function A(this: any) { this.value = 7; }
            const Forward: any = new Proxy(A, {});
            console.log(new Forward().value);
            """ : """
            const pair: any = Proxy.revocable({ value: 7 }, {});
            console.log(pair.proxy.value);
            """;
        using var directory = CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", source);
        var output = directory.GetPath("main.dll");
        var compile = CliTestHelper.RunCli(
            $"--no-tsconfig --compile \"{path}\" -o \"{output}\" --verify{(standalone ? " --standalone" : "")}",
            directory.Path, TimeSpan.FromSeconds(60));
        Assert.True(compile.ExitCode == 0, compile.StandardOutput + compile.StandardError);
        Assert.Contains("IL verification passed.", compile.StandardOutput);
        Assert.Equal(!standalone, File.Exists(directory.GetPath("SharpTS.dll")));

        var result = CaptureBoundedProcess(output, hidden: true);
        if (standalone)
        {
            Assert.NotEqual(0, result.ExitCode);
            Assert.Empty(result.Stdout);
            Assert.Contains("Proxy", result.Stderr);
            Assert.Contains("SharpTS.dll", result.Stderr);
            Assert.Contains("--standalone", result.Stderr);
        }
        else
        {
            Assert.Equal(0, result.ExitCode);
            Assert.Equal("7\n", result.Stdout);
            Assert.Empty(result.Stderr);
        }
    }
}
