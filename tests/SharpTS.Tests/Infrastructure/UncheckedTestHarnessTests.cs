using Xunit;

namespace SharpTS.Tests.Infrastructure;

public sealed class UncheckedTestHarnessTests
{
    [Theory]
    [InlineData(ExecutionMode.Interpreted)]
    [InlineData(ExecutionMode.Compiled)]
    public void ExplicitUncheckedRunnerBypassesChecker(ExecutionMode mode)
    {
        const string source = "const value: number = 'javascript'; console.log(value);";
        Assert.ThrowsAny<Exception>(() => TestHarness.Run(source, mode));
        Assert.Equal("javascript\n", TestHarness.RunUnchecked(source, mode));
    }
}
