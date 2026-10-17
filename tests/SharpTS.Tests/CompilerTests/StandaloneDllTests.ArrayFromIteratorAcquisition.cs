using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Fact]
    public void Isolated_Issue2136MappedArrayFrom_RetainsAcquisitionAndInterleaving()
    {
        var row = ArrayFromIteratorAcquisitionTests.Cases().Single(row => (string)row[0] == "mapped-object");
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", (string)row[1]);
        AssertStandaloneConstructorIdentity(directory.Path, path,
            directory.GetPath("array-from-retained-acquisition.dll"), (string)row[2]);
    }
}
