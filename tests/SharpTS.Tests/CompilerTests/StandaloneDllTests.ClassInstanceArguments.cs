using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Isolated_ClassInstanceArguments_PreserveReferenceSlotsAndReplacementObjects(bool replacement)
    {
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var source = replacement ? ClassInstanceArgumentTests.ReplacementSource : ClassInstanceArgumentTests.Source;
        var expected = replacement ? ClassInstanceArgumentTests.ReplacementExpected : ClassInstanceArgumentTests.Expected;
        var path = directory.CreateFile("main.ts", source);
        var output = directory.GetPath("class-instance-arguments.dll");
        AssertStandaloneConstructorIdentity(directory.Path, path, output, expected);
    }
}
