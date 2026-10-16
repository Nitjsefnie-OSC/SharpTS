using SharpTS.Tests.IntegrationTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Isolated_Issue2199StaticOverride_VerifiesBeforeExecution(bool standalone)
    {
        const string source = """
            class Base { static get x(): number { return 1; } }
            class Child extends Base { static override get x(): number { return 2; } }
            console.log(Child.x);
            class Animal { static speak(): string { return "..."; } }
            class Dog extends Animal { static override speak(): string { return "Woof"; } }
            console.log(Dog.speak());
            """;
        using var directory = CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", source);
        var output = directory.GetPath("static-override.dll");
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
        Assert.Equal("2\nWoof\n", result.Stdout);
        Assert.Empty(result.Stderr);
    }
}
