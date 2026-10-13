using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("issue")]
    [InlineData("getters")]
    [InlineData("receivers")]
    [InlineData("awaited-iterable")]
    [InlineData("pending")]
    [InlineData("generator")]
    public void Isolated_Issue2054AsyncClassConstructorIteration(string name)
    {
        var row = AsyncClassConstructorIterationTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        AssertStandaloneConstructorIdentity(directory.Path, path,
            directory.GetPath("async-class-iteration-" + name + ".dll"), (string)row[2]);
    }
}
