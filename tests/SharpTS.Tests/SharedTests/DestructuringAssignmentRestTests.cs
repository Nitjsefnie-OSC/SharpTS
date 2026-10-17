using SharpTS.Parsing;
using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class DestructuringAssignmentRestTests
{
    public const string IssueSource = """
        let rest: any = [], value: any = 0;
        [...rest, value] = [1, 2];
        console.log(rest.join(","), value);
        """;

    public const string ObjectNonfinalSource = """
        let rest: any = {}, value: any = 0; ({...rest, value} = {value: 2, extra: 1}); console.log(rest.extra, value);
        """;

    public const string NestedArraySource = """
        let rest: any = [], value: any = 0; [[...rest, value]] = [[1, 2]]; console.log(rest.join(","), value);
        """;

    public const string NestedObjectSource = """
        let rest: any = {}, value: any = 0; ({item: {...rest, value}} = {item: {value: 2, extra: 1}}); console.log(rest.extra, value);
        """;

    public const string ArrayTrailingCommaSource = """
        let rest: any = []; [...rest,] = [1, 2]; console.log(rest.join(","));
        """;

    public const string ObjectTrailingCommaSource = """
        let rest: any = {}; ({...rest,} = {value: 2}); console.log(rest.value);
        """;

    public const string RestBeforeHoleSource = """
        let rest: any = []; [...rest,,] = [1, 2]; console.log(rest.join(","));
        """;

    public const string MultipleRestSource = """
        let rest: any = [], other: any = []; [...rest, ...other] = [1, 2]; console.log(rest.join(","), other.length);
        """;

    public const string NestedDefaultSource = """
        let rest: any = [], value: any = 0; [[...rest, value] = []] = [[1, 2]]; console.log(rest.join(","), value);
        """;

    public const string LegalPatternsSource = """
        let head: any = 0, rest: any = [], value: any = 0, objectRest: any = {}; [head, ...rest] = [1, 2, 3]; ({head: value, ...objectRest} = {head: 4, extra: 5}); console.log(head, rest.join(","), value, objectRest.extra); let first: any = 0, second: any = 0; [...[first, second]] = [5, 6]; console.log(first, second);
        """;

    public const string LegalLiteralsSource = """
        const array = [...[1], 2, ...[3], 4,]; const object = {...{first: 1}, second: 2, ...{third: 3},}; console.log(array.join(","), object.first, object.second, object.third);
        """;

    public const string RawDefaultReadSource = """
        let head: any = 0, tail: any = [];
        const target: any = { item: 0 }, reads: any = { key: "item" };
        const state = { calls: 0 };
        let original: any;
        function source(): any { state.calls++; original = [undefined]; return original; }
        const result: any = ([[head, ...tail] = [...[1], 2, ...[3],]] = source());
        ({ [reads?.key]: target.item = ({ ...{ item: 7 }, extra: 8 }).item } = {});
        console.log(head, tail.join(","), state.calls, result === original, target.item);
        """;

    public const string InvalidLoopSource = """
        let rest: any = [], value: any = 0; for ([...rest, value] of [[1, 2]]) console.log(rest.join(","), value);
        """;

    public const string InvalidAsyncLoopSource = """
        async function check() { let rest: any = [], value: any = 0; for await ([...rest, value] of [[1, 2]]) {} }
        """;

    public const string GroupedLoopSource = """
        let value: any = 0; for (([value]) of [[1]]) {}
        """;

    public const string ObjectRestPatternLoopSource = """
        let value: any = 0; for ({...{value}} of [{value: 1}]) {}
        """;

    public const string PreservationSource = """
        async function check() {
            let value: any = 0, rest: any[] = [];
            for ([, value = 7, ...rest] of [[0, undefined, 8]]) { let value = 9; }
            for await ([, value = 7, ...rest] of [[0, undefined, 8]]) {}
            for ([value] = [1]; value < 2; value++) {}
            let size: any;
            [...{length: size}] = [1, 2];
        }
        """;

    public static IEnumerable<object[]> InvalidCases()
    {
        yield return ["issue", IssueSource];
        yield return ["object-nonfinal", ObjectNonfinalSource];
        yield return ["nested-array", NestedArraySource];
        yield return ["nested-object", NestedObjectSource];
        yield return ["array-trailing-comma", ArrayTrailingCommaSource];
        yield return ["object-trailing-comma", ObjectTrailingCommaSource];
        yield return ["rest-before-hole", RestBeforeHoleSource];
        yield return ["multiple-rest", MultipleRestSource];
        yield return ["nested-default", NestedDefaultSource];
    }

    [Theory, MemberData(nameof(InvalidCases))]
    public void InvalidRestPositionsAreRejectedBeforeEvaluation(string name, string source)
    {
        Assert.NotEmpty(name);
        AssertRejected(source, "must be last");
    }

    public static IEnumerable<object[]> InvalidLoopCases()
    {
        yield return ["for-of-rest-position", InvalidLoopSource, "must be last"];
        yield return ["for-await-rest-position", InvalidAsyncLoopSource, "must be last"];
        yield return ["grouped-loop-pattern", GroupedLoopSource, "cannot be parenthesized"];
        yield return ["object-rest-pattern-target", ObjectRestPatternLoopSource, "Object rest must have an assignment target"];
    }

    [Theory, MemberData(nameof(InvalidLoopCases))]
    public void LoopSpecificPatternRestrictionsRemainRejected(string name, string source, string diagnostic)
    {
        Assert.NotEmpty(name);
        AssertRejected(source, diagnostic);
    }

    [Fact]
    public void ValidLoopHeadsAndArrayRestObjectPatternsRemainAccepted()
    {
        var result = new Parser(new Lexer(PreservationSource).ScanTokens()).Parse();
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
    }

    public static IEnumerable<object[]> Cases()
    {
        yield return ["legal-final-rest-patterns", LegalPatternsSource, "1 2,3 4 5\n5 6\n"];
        yield return ["ordinary-spread-literals", LegalLiteralsSource, "1,2,3,4 1 2 3\n"];
        yield return ["raw-default-and-optional-read-boundaries", RawDefaultReadSource, "1 2,3 1 true 7\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ValidPatternsAndReadLiteralsPreserveAssignmentSemantics(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    private static void AssertRejected(string source, string expectedDiagnostic)
    {
        var result = new Parser(new Lexer(source).ScanTokens()).Parse();
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains(expectedDiagnostic, StringComparison.OrdinalIgnoreCase));
    }
}