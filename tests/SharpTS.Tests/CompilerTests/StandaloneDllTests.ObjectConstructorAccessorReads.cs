using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("issue")]
    [InlineData("receivers")]
    [InlineData("missing-values")]
    [InlineData("getterless-issue")]
    [InlineData("undefined-getter")]
    public void Isolated_Issue2047ObjectConstructorAccessorReads(string name)
    {
        var row = ObjectConstructorAccessorReadTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        AssertStandaloneConstructorIdentity(directory.Path, path,
            directory.GetPath("object-accessor-" + name + ".dll"), (string)row[2]);
    }
}
