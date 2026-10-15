using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.IntegrationTests;

public sealed class CliNewTargetTests
{
    [Theory]
    [MemberData(nameof(NewTargetTests.Cases), MemberType = typeof(NewTargetTests))]
    public void ResolverAssistedInterpreterPreservesConstructionContext(string name, string source, string expected)
    {
        using var directory = CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile(name + ".ts", source);
        var result = CliTestHelper.RunCli($"--no-tsconfig \"{path}\"", directory.Path);
        Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
        Assert.Equal(expected, result.StandardOutput);
    }
}
