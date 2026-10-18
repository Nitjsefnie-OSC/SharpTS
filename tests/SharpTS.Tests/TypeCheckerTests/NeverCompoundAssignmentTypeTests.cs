using SharpTS.Tests.Infrastructure;
using SharpTS.TypeSystem;
using SharpTS.TypeSystem.Exceptions;
using Xunit;

namespace SharpTS.Tests.TypeCheckerTests;

public sealed class NeverCompoundAssignmentTypeTests
{
    public static IEnumerable<object[]> NumericResultCases()
    {
        foreach (string op in new[] { "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=", ">>=", ">>>=" })
        {
            foreach (string target in new[] { "value", "numbers[0]" })
            {
                yield return [op, target, $$"""
                    function fail(): never { throw "sentinel"; }
                    function probe() {
                        let value: number = 8;
                        const numbers: number[] = [8];
                        const result: string = ({{target}} {{op}} fail());
                    }
                    """];
            }
        }
    }

    [Theory, MemberData(nameof(NumericResultCases))]
    public void NeverRightOperandKeepsTheNumericCompoundResultType(string op, string target, string source)
    {
        Assert.NotEmpty(op);
        Assert.NotEmpty(target);
        var error = Assert.ThrowsAny<TypeCheckException>(() =>
            new TypeChecker().Check(TestHarness.ParseOrThrow(source)));
        Assert.Equal("TS2322", error.Diagnostic.TsCode);
    }

    public static IEnumerable<object[]> InvalidOperandCases()
    {
        foreach (string op in new[] { "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=", ">>=", ">>>=" })
        {
            foreach (string operand in new[] { "true", "{}", "'text'" })
            {
                yield return [op, "right", $$"""
                    let value: number = 8;
                    value {{op}} {{operand}};
                    """];
                yield return [op, "left", $$"""
                    function fail(): never { throw "sentinel"; }
                    let value = {{operand}};
                    value {{op}} fail();
                    """];
            }
        }
    }

    [Theory, MemberData(nameof(InvalidOperandCases))]
    public void AcceptingNeverDoesNotAcceptOtherInvalidNumericOperands(string op, string side, string source)
    {
        Assert.NotEmpty(op);
        var error = Assert.ThrowsAny<TypeCheckException>(() =>
            new TypeChecker().Check(TestHarness.ParseOrThrow(source)));
        Assert.Equal(side == "left" ? "TS2362" : "TS2363", error.Diagnostic.TsCode);
    }

    [Fact]
    public void RecoveryReportsOnlyTheInvalidLeftOperandWhenTheRightIsNever()
    {
        const string source = """
            function fail(): never { throw "sentinel"; }
            let value: boolean = true;
            value -= fail();
            """;

        var result = new TypeChecker().CheckWithRecovery(TestHarness.ParseOrThrow(source));
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("TS2362", diagnostic.TsCode);
        Assert.Equal(3, diagnostic.Line);
    }
}
