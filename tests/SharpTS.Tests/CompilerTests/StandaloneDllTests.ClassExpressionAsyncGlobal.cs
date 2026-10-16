using SharpTS.Tests.IntegrationTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Isolated_Issue2196ClassExpressionAsyncGlobal_VerifiesBeforeExecution(bool standalone)
    {
        const string source = """
            class A { value(): any { return 'declared'; } }
            const B: any = class extends A {
                label = 'child';
                make() { return async () => { await Promise.resolve(0); return this.label; }; }
            };
            const base: any = A.prototype;
            Object.defineProperty(base, 'value', { get() { return this.label; }, configurable: true });
            const read: any = new B().make();
            read.call({ label: 'fake' }).then((value: any) => console.log(value))
                .catch((error: any) => console.log(error.name, error.message));
            """;
        using var directory = CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", source);
        var output = directory.GetPath("class-expression-async-global.dll");
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
        Assert.Equal("child\n", result.Stdout);
        Assert.Empty(result.Stderr);
    }
}
