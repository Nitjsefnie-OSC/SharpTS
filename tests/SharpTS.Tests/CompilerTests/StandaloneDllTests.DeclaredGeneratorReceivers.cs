using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("sync")]
    [InlineData("protocols")]
    public void Isolated_Issue2137DeclaredGeneratorReceivers_PreserveInvocationAndFunctionIdentity(string name)
    {
        var row = DeclaredGeneratorReceiverTests.Cases().Single(row => (string)row[0] == name);
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        AssertStandaloneConstructorIdentity(directory.Path, path,
            directory.GetPath("declared-generator-receiver-" + name + ".dll"), (string)row[2]);
    }
}
