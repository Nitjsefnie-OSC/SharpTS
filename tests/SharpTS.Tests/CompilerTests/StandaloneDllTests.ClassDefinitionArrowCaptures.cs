using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("mixed")]
    [InlineData("shadows")]
    [InlineData("async-mixed")]
    public void Isolated_ClassDefinitionArrowCapturesRetainTheirLiveBindings(string name)
    {
        var row = ClassDefinitionArrowCaptureTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        AssertStandaloneConstructorIdentity(directory.Path, path,
            directory.GetPath("class-definition-capture-" + name + ".dll"), (string)row[2]);
    }
}
