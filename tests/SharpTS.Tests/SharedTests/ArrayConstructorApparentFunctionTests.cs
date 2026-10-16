using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ArrayConstructorApparentFunctionTests
{
    public const string IssueSource = """
        const Bound = Array.bind(null, 7, 8);
        console.log(Bound().join(","));
        """;

    public const string AliasSource = """
        const A = Array;
        console.log(A.call(null, 1, 2).join(","), A["apply"](null, [3, 4]).join(","),
            A["bind"](null, 5, 6)().join(","));
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "7,8\n"];
        yield return ["inferred-alias", AliasSource, "1,2 3,4 5,6\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void UncastArrayHelpersPreserveCallableBehavior(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
