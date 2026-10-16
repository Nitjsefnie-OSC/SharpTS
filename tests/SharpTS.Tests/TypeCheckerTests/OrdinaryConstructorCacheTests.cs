using SharpTS.Parsing;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.TypeCheckerTests;

public sealed class OrdinaryConstructorCacheTests
{
    public static IEnumerable<object[]> ParentComparisons()
    {
        foreach (string parent in new[] { "record", "function", "array", "union" })
        foreach (bool sameIdentity in new[] { false, true })
        foreach (bool badFirst in new[] { false, true })
            yield return [parent, sameIdentity, badFirst];
    }

    private static string Source(string parent, bool sameIdentity, string calls)
    {
        string good = sameIdentity ? "Target" : "Good";
        var (values, parameter) = parent switch
        {
            "record" => ($$"""
                const goodValue = { constructorValue: {{good}} };
                const badValue = { constructorValue: Bad };
                """, "{ constructorValue: typeof Target }"),
            "function" => ($$"""
                const goodValue = (): typeof {{good}} => {{good}};
                const badValue = (): typeof Bad => Bad;
                """, "() => typeof Target"),
            "array" => ($$"""
                const goodValue = [{ constructorValue: {{good}} }];
                const badValue = [{ constructorValue: Bad }];
                """, "{ constructorValue: typeof Target }[]"),
            "union" => ($$"""
                const goodValue: { constructorValue: typeof {{good}} | null } = { constructorValue: {{good}} };
                const badValue: { constructorValue: typeof Bad | null } = { constructorValue: Bad };
                """, "{ constructorValue: typeof Target | null }"),
            _ => throw new ArgumentOutOfRangeException(nameof(parent))
        };
        return """
            const Target = class Same { static marker: number = 1; };
            const Good = class Same { static marker: number = 2; };
            const Bad = class Same { static marker: string = "bad"; };
            """ + "\n" + values + "\nfunction take(value: " + parameter + "): void {}\n" + calls;
    }

    [Theory, MemberData(nameof(ParentComparisons))]
    public void ParentCachesPreserveNestedConstructorIdentityAcrossComparisonOrder(
        string parent, bool sameIdentity, bool badFirst)
    {
        string calls = badFirst ? "take(badValue);\ntake(goodValue);" : "take(goodValue);\ntake(badValue);";
        var statements = new Parser(new Lexer(Source(parent, sameIdentity, calls)).ScanTokens()).ParseOrThrow();
        var diagnostics = new TypeChecker(new TypeCheckerOptions { StrictFunctionTypes = true, MaxErrors = 50 })
            .CheckWithRecovery(statements).Diagnostics;
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("TS2345", diagnostic.TsCode);
        Assert.Equal(badFirst ? 7 : 8, diagnostic.Line);
    }

    [Theory]
    [InlineData("record")]
    [InlineData("function")]
    [InlineData("array")]
    [InlineData("union")]
    public void UnprimedNestedConstructorMismatchIsRejected(string parent)
    {
        var statements = new Parser(new Lexer(Source(parent, false, "take(badValue);")).ScanTokens()).ParseOrThrow();
        var diagnostics = new TypeChecker(new TypeCheckerOptions { StrictFunctionTypes = true, MaxErrors = 50 })
            .CheckWithRecovery(statements).Diagnostics;
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("TS2345", diagnostic.TsCode);
        Assert.Equal(7, diagnostic.Line);
    }
}
