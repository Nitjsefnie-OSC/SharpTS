using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("issue")]
    [InlineData("cached-builtin")]
    [InlineData("descriptors")]
    public void Isolated_Issue2126ReflectDeletesObjectConstructorProperties(string name)
    {
        var row = ReflectObjectConstructorDeletionTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        AssertStandaloneConstructorIdentity(directory.Path, path,
            directory.GetPath("reflect-object-delete-" + name + ".dll"), (string)row[2]);
    }
}
