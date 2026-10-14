using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("issue")]
    [InlineData("controls")]
    public void Isolated_Issue2051ApplyUndefinedArgumentLists(string name)
    {
        var row = ApplyUndefinedArgumentListTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        AssertStandaloneConstructorIdentity(directory.Path, path,
            directory.GetPath("apply-undefined-" + name + ".dll"), (string)row[2]);
    }
}
