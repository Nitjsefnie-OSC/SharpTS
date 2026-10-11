using Xunit;

namespace SharpTS.Test262;

[Collection("Issue 1279 parity")]
public sealed class Test262ExceptionClassificationTests
{
    [Theory]
    [InlineData(Test262ExecutionMode.Interpreted)]
    [InlineData(Test262ExecutionMode.Compiled)]
    public void Guest_assertions_preserve_their_failure_message(Test262ExecutionMode mode)
    {
        var result = Run("throw new Test262Error('specific assertion failure');", mode);

        Assert.Equal(Test262Outcome.Fail, result.Outcome);
        Assert.Contains("specific assertion failure", result.Message);
    }

    [Theory]
    [InlineData(Test262ExecutionMode.Interpreted)]
    [InlineData(Test262ExecutionMode.Compiled)]
    public void Guest_runtime_errors_preserve_their_message(Test262ExecutionMode mode)
    {
        var result = Run("throw new Error('specific runtime failure');", mode);

        Assert.Equal(Test262Outcome.RuntimeError, result.Outcome);
        Assert.Contains("specific runtime failure", result.Message);
    }

    [Theory]
    [InlineData(Test262ExecutionMode.Interpreted)]
    [InlineData(Test262ExecutionMode.Compiled)]
    public void Primitive_throws_remain_runtime_errors(Test262ExecutionMode mode)
    {
        var result = Run("throw 'primitive failure';", mode);

        Assert.Equal(Test262Outcome.RuntimeError, result.Outcome);
        Assert.Contains("primitive failure", result.Message);
    }

    private static Test262Result Run(string source, Test262ExecutionMode mode)
    {
        var root = Test262Paths.TryFindRoot();
        Assert.NotNull(root);
        var path = Path.Combine(Path.GetTempPath(), $"sharpts-test262-{Guid.NewGuid():N}.js");
        File.WriteAllText(path, source);
        try
        {
            var runner = new Test262Runner(root, TimeSpan.FromSeconds(15), useNonCollectibleLoad: true);
            return runner.RunOne(path, mode);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
