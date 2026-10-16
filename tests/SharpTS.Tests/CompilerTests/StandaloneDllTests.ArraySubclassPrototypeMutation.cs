using SharpTS.Tests.IntegrationTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Isolated_Issue2190ArraySubclassPrototypeMutation_VerifiesBeforeExecution(bool standalone)
    {
        const string source = """
            class ArrayParent extends Array {}
            class ArrayChild extends ArrayParent {}
            const array: any = new ArrayChild(3, 4);
            const original = Object.getPrototypeOf(array);
            Object.setPrototypeOf(array, null);
            console.log(array instanceof ArrayChild, array instanceof ArrayParent,
                array instanceof Array, array instanceof Object);
            console.log(Array.isArray(array), array.length, array[0], array[1]);
            Object.setPrototypeOf(array, original);
            console.log(array instanceof ArrayChild, array instanceof ArrayParent,
                array instanceof Array, array instanceof Object);
            console.log(array.push(5), array.length, array[2]);
            """;
        using var directory = CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", source);
        var output = directory.GetPath("array-subclass-prototype-mutation.dll");
        var compile = CliTestHelper.RunCli(
            $"--no-tsconfig --compile \"{path}\" -o \"{output}\" --verify{(standalone ? " --standalone" : "")}",
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
        Assert.Equal("false false false false\ntrue 2 3 4\ntrue true true true\n3 3 5\n", result.Stdout);
        Assert.Empty(result.Stderr);
    }
}
