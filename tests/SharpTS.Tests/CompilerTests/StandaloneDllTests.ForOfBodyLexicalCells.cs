using SharpTS.Tests.Infrastructure;
using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory, MemberData(nameof(ForOfBodyLexicalCellTests.Cases), MemberType = typeof(ForOfBodyLexicalCellTests))]
    public void Isolated_Issue2148ForOfBodyLexicalCells_ShareWithinEachActivation(
        string name, string source, string expected)
    {
        Assert.NotEmpty(name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", source);
        AssertStandaloneConstructorIdentity(directory.Path, path,
            directory.GetPath("for-of-body-cells-" + name + ".dll"), expected);
    }

    [Theory, MemberData(nameof(ForOfBodyLexicalCellTests.RuntimeOnlyCases), MemberType = typeof(ForOfBodyLexicalCellTests))]
    public void Isolated_Issue2148ForwardBodyClosures_VerifyBeforeExecution(
        string name, string source, string expected)
    {
        Assert.NotEmpty(name);
        var bytes = TestHarness.CompileUncheckedAndVerify(source);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var output = directory.GetPath("forward-body-cells.dll");
        File.WriteAllBytes(output, bytes);
        directory.CreateFile("forward-body-cells.runtimeconfig.json", """
            { "runtimeOptions": { "tfm": "net10.0", "framework": {
                "name": "Microsoft.NETCore.App", "version": "10.0.0"
            } } }
            """);
        Assert.DoesNotContain("SharpTS", GetAssemblyReferences(output));
        Assert.False(File.Exists(directory.GetPath("SharpTS.dll")));
        Assert.Equal(expected, ExecuteCompiledDllIsolated(output, timeoutMs: 30000,
            verifyStandardError: error => Assert.Empty(error), standardInput: ""));
    }
}
