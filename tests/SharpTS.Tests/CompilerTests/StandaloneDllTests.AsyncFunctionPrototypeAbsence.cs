using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("issue")]
    [InlineData("captured")]
    public void Isolated_Issue2138AsyncFunctions_HaveNoOwnPrototype(string name)
    {
        var row = AsyncFunctionPrototypeAbsenceTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        AssertStandaloneConstructorIdentity(directory.Path, path,
            directory.GetPath("async-function-prototype-" + name + ".dll"), (string)row[2]);
    }
}
