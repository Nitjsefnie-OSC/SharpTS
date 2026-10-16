using SharpTS.Tests.IntegrationTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Isolated_Issue2189GenericInstanceConstructors_VerifyBeforeExecution(bool standalone)
    {
        const string source = """
            class Box<T> {}
            const numberBox: any = new Box<number>();
            const stringBox: any = new Box<string>();
            console.log(numberBox.constructor === Box, stringBox.constructor === Box,
                numberBox.constructor === stringBox.constructor);
            console.log(Box.prototype.constructor === Box,
                Object.getPrototypeOf(numberBox).constructor === Box,
                Object.getPrototypeOf(stringBox).constructor === Box);
            class GenericPromise<T> extends Promise<T> {}
            const direct: any = new GenericPromise<number>((resolve: any) => resolve(3));
            const inherited: any = GenericPromise.resolve(4);
            console.log(direct.constructor === GenericPromise, inherited.constructor === GenericPromise,
                GenericPromise.prototype.constructor === GenericPromise);
            """;
        using var directory = CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", source);
        var output = directory.GetPath("generic-instance-constructor.dll");
        // Isolate the separate Promise declaration-library boundary tracked by #2169.
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
        Assert.Equal("true true true\ntrue true true\ntrue true true\n", result.Stdout);
        Assert.Empty(result.Stderr);
    }
}
