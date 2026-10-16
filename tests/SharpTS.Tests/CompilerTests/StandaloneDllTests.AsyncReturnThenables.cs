using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData("private-field")]
    [InlineData("preservation")]
    public void Isolated_Issue2146AsyncReturnThenables_PreserveSupportedReturnPaths(string name)
    {
        var (source, expected) = name switch
        {
            "private-field" => (PrivateCallableFieldTests.AsyncReturnThenableSource, "47\n"),
            "preservation" => (AsyncReturnThenableTests.PreservationSource, AsyncReturnThenableTests.PreservationExpected),
            _ => throw new ArgumentException("Unknown return-path case", nameof(name))
        };
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", source);
        AssertStandaloneConstructorIdentity(directory.Path, path,
            directory.GetPath("async-return-thenable-" + name + ".dll"), expected);
    }
}
