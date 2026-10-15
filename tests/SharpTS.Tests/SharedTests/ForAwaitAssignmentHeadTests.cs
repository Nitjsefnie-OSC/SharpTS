using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ForAwaitAssignmentHeadTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["elisions", """
            async function run() {
                let count = 0;
                for await ([,] of [[]]) count++;
                for await ([] of [[], []]) count++;
                console.log(count);
            }
            run();
            """, "3\n"];
        yield return ["assignments", """
            async function run() {
                let value = 0, rest: any[] = [];
                for await ([, value = 7, ...rest] of [[0, undefined, 8, 9], [0, 3, 4]])
                    console.log(value, rest.join(","));
                console.log(value, rest.join(","));
            }
            run();
            """, "7 8,9\n3 4\n3 4\n"];
        yield return ["scope", """
            async function run() {
                let value = 0;
                for await ([value] of [[1]]) { let value = 9; console.log(value); }
                console.log(value);
                for ([value] of [[2], [3]]) {}
                console.log(value);
                for ([value] = [1]; value < 2; value++) {}
                console.log(value);
            }
            run();
            """, "9\n1\n3\n2\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void AssignmentPatternsInForAwaitHeadsUseExistingTargets(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory]
    [InlineData("async function run() { for await ([1] of [[]]) {} }")]
    [InlineData("async function run() { let rest, other; for await ([...rest, other] of [[]]) {} }")]
    [InlineData("async function run() { let rest; for await ([...rest,] of [[]]) {} }")]
    [InlineData("async function run() { let rest; for await ({...rest,} of [{}]) {} }")]
    [InlineData("async function run() { let value; for await ({...{value}} of [{value: 1}]) {} }")]
    [InlineData("async function run() { let value; for await (([value]) of [[1]]) {} }")]
    [InlineData("async function run() { let value; for await (value in {}) {} }")]
    [InlineData("async function run() { for await ([]; false;) {} }")]
    public void InvalidAssignmentHeadsAndAsyncLoopFormsRemainRejected(string source)
    {
        var parser = new SharpTS.Parsing.Parser(new SharpTS.Parsing.Lexer(source).ScanTokens());
        Assert.False(parser.Parse().IsSuccess);
    }

    [Fact]
    public void TrailingCommasAfterSpreadRemainValidInOrdinaryLiterals()
    {
        const string source = "const array = [...[1, 2],]; const object = {...{value: 1},};";
        var parser = new SharpTS.Parsing.Parser(new SharpTS.Parsing.Lexer(source).ScanTokens());
        Assert.True(parser.Parse().IsSuccess);
    }
}
