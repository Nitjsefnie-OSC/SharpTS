using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("issue")]
    [InlineData("parameter-self")]
    [InlineData("constructor")]
    [InlineData("mutable-declaration")]
    [InlineData("mutable-expression")]
    [InlineData("mutable-nested-method")]
    [InlineData("mutable-arrow-factory")]
    [InlineData("static")]
    public void Isolated_Issue2041MethodArrowCapturesRetainSharedStorage(string name)
    {
        var row = NestedClassMethodArrowCaptureTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        AssertStandaloneConstructorIdentity(directory.Path, path,
            directory.GetPath("method-capture-" + name + ".dll"), (string)row[2]);
    }

    [Fact]
    public void Isolated_Issue2041ModuleMethodArrowCapturesRetainSharedStorage()
    {
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        foreach (var (file, source) in NestedClassMethodArrowCaptureTests.ModuleFiles())
            directory.CreateFile(file, source);
        AssertStandaloneConstructorIdentity(directory.Path, directory.GetPath("main.ts"),
            directory.GetPath("method-capture-module.dll"), "true 2 false\ntrue 3 true\ntrue 2 false\n");
    }
}
