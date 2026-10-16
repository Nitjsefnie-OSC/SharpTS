using SharpTS.Parsing;
using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ForAwaitIdentifierHeadTests
{
    [Theory]
    [InlineData("key in { x: 1 }")]
    [InlineData("let key in { x: 1 }")]
    [InlineData("const key in { x: 1 }")]
    [InlineData("var key in { x: 1 }")]
    public void AsyncForInIsRejectedBeforeExecution(string head)
    {
        var source = $"async function run() {{ let key: any; for await ({head}) console.log(key); }} run();";
        var parser = new Parser(new Lexer(source).ScanTokens());
        Assert.False(parser.Parse().IsSuccess);
    }

    [Theory, ModeData]
    public void ExistingIdentifierSupportsSyncForInAndAsyncForOf(ExecutionMode mode)
    {
        const string source = """
            async function run() {
                let key: any;
                for (key in { x: 1 }) console.log(key);
                for await (key of [Promise.resolve("first"), "last"]) console.log(key);
            }
            run();
            """;
        Assert.Equal("x\nfirst\nlast\n", TestHarness.Run(source, mode));
    }
}
