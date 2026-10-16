using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.IntegrationTests;

public sealed class CliArrayApparentFunctionTests
{
    [Fact]
    public void CheckedInterpreterExecutesDirectArrayBindWithDefaultLibraries()
    {
        using var directory = CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", ArrayConstructorApparentFunctionTests.IssueSource);
        var result = CliTestHelper.RunCli($"--no-tsconfig \"{path}\"", directory.Path);
        Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
        Assert.Equal("7,8\n", result.StandardOutput);
        Assert.Empty(result.StandardError);
    }
}
