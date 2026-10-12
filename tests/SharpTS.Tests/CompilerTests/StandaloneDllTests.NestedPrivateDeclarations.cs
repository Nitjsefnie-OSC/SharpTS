using SharpTS.Tests.SharedTests;
using SharpTS.Tests.Infrastructure;
using SharpTS.Parsing;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Fact]
    public void Issue2010_NestedDeclarationPreservesNamespaceAndDecoratorMetadata()
    {
        var (assembly, output) = TestHarness.CompileAndRun(NestedPrivateDeclarationTests.MetadataControlSource, DecoratorMode.Legacy);
        Assert.Equal("42 43\n", output);
        var inner = Assert.Single(assembly.GetTypes(), type => type.Name == "Inner");
        Assert.StartsWith("NestedOwners.", inner.FullName);
        var decorated = Assert.Single(assembly.GetTypes(), type => type.Name.EndsWith("_Decorated", StringComparison.Ordinal));
        Assert.Equal("NestedOwners", decorated.Namespace);
        var attribute = Assert.Single(decorated.GetCustomAttributes(typeof(ObsoleteAttribute), false).Cast<ObsoleteAttribute>());
        Assert.Equal("nested declaration metadata", attribute.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Isolated_Issue2010NestedDeclaration_PreservesPrivateOwner(bool genericExpression)
    {
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var source = genericExpression ? NestedPrivateDeclarationTests.GenericExpressionIssueSource
            : NestedPrivateDeclarationTests.IssueSource;
        var path = directory.CreateFile("main.ts", source);
        var output = directory.GetPath("nested-private.dll");
        var compile = IntegrationTests.CliTestHelper.RunCli(
            $"--no-tsconfig --compile \"{path}\" -o \"{output}\" --standalone --verify",
            directory.Path, TimeSpan.FromSeconds(60));
        Assert.True(compile.ExitCode == 0, compile.StandardOutput + compile.StandardError);
        Assert.Contains("IL verification passed.", compile.StandardOutput);
        Assert.DoesNotContain("SharpTS", GetAssemblyReferences(output));
        Assert.False(File.Exists(directory.GetPath("SharpTS.dll")));
        Assert.Equal("42\n", ExecuteCompiledDllIsolated(output, timeoutMs: 30000,
            verifyStandardError: error => Assert.Empty(error), standardInput: ""));
    }
}
