using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("function")]
    [InlineData("arrow")]
    [InlineData("labels")]
    public void Isolated_AsyncStableIteratorResultConsumers(string name)
    {
        var row = AsyncStableIteratorResultTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        AssertStandaloneConstructorIdentity(directory.Path, path,
            directory.GetPath("async-stable-iterator-" + name + ".dll"), (string)row[2]);
    }
}
