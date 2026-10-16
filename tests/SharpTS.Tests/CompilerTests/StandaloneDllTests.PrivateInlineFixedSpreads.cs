using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Fact]
    public void Isolated_Issue2119PrivateInlineFixedSpreads_PreservePositionalArguments()
    {
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", PrivateInlineFixedSpreadTests.StaticSource);
        AssertStandaloneConstructorIdentity(directory.Path, path,
            directory.GetPath("private-inline-fixed-spreads.dll"), PrivateInlineFixedSpreadTests.StaticExpected);
    }
}
