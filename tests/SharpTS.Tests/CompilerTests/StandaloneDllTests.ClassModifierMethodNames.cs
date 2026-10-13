using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("issue")]
    [InlineData("expression")]
    [InlineData("generic-static")]
    public void Isolated_Issue2042ClassModifierMethodNames(string name)
    {
        var row = ClassModifierMethodNameTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        AssertStandaloneConstructorIdentity(directory.Path, path,
            directory.GetPath("keyword-method-" + name + ".dll"), (string)row[2]);
    }
}
