using SharpTS.Tests.IntegrationTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Isolated_Issue2197ClassMethodThisCoercion_VerifiesBeforeExecution(bool standalone)
    {
        const string source = """
            const C = class {
                value = 7;
                read() { return this.value; }
            };
            const instance: any = new C(), read: any = instance.read;
            try { console.log('bare', 'returned', read()); }
            catch (error: any) { console.log('bare', 'threw', error instanceof TypeError); }
            function invoke(name: string, receiver: any) {
                try { console.log(name, 'returned', read.call(receiver)); }
                catch (error: any) { console.log(name, 'threw', error instanceof TypeError); }
            }
            invoke('undefined', undefined);
            invoke('null', null);
            invoke('object', { value: 13 });
            """;
        using var directory = CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", source);
        var output = directory.GetPath("class-method-this-coercion.dll");
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
        Assert.Equal("bare threw true\nundefined threw true\nnull threw true\nobject returned 13\n", result.Stdout);
        Assert.Empty(result.Stderr);
    }
}
