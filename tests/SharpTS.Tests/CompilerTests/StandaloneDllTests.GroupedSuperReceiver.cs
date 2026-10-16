using SharpTS.Tests.IntegrationTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Isolated_Issue2195GroupedSuperReceiver_VerifiesBeforeExecution(bool standalone)
    {
        const string source = """
            class A { label = 'a'; value() { return this.label; } }
            class B extends A {
                label = 'b';
                read() { return (() => (super.value)())(); }
                asserted() { return (() => (super.value as any)())(); }
            }
            class C extends B { label = 'c'; }
            console.log(new C().read(), new C().asserted());
            const home: any = B.prototype, receiver: any = { label: 'borrowed' };
            console.log(home.read.call(receiver), home.asserted.call(receiver));
            """;
        using var directory = CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", source);
        var output = directory.GetPath("grouped-super-receiver.dll");
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
        Assert.Equal("c c\nborrowed borrowed\n", result.Stdout);
        Assert.Empty(result.Stderr);
    }
}
