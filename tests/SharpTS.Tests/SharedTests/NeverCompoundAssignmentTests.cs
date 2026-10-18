using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class NeverCompoundAssignmentTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (string op in new[] { "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=", ">>=", ">>>=" })
        {
            foreach (string rhs in new[] { "fail()", "arrow()" })
            {
                yield return [op, rhs, $$"""
                    let trace = "";
                    const sentinel: any = {};
                    function fail(): never { trace += "r"; throw sentinel; }
                    const arrow = (): never => { trace += "r"; throw sentinel; };
                    let value: number = 8;
                    const numbers: number[] = [8];
                    const dynamic: any[] = [8];
                    function numberArray(): number[] { trace += "a"; return numbers; }
                    function dynamicArray(): any[] { trace += "a"; return dynamic; }
                    function index(): number { trace += "i"; return 0; }
                    try { value {{op}} {{rhs}}; }
                    catch (error) { console.log(error === sentinel, value, trace); }
                    trace = "";
                    try { numberArray()[index()] {{op}} {{rhs}}; }
                    catch (error) { console.log(error === sentinel, numbers[0], trace); }
                    trace = "";
                    try { dynamicArray()[index()] {{op}} {{rhs}}; }
                    catch (error) { console.log(error === sentinel, dynamic[0], trace); }
                    """];
            }
        }
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void NeverRightOperandThrowsBeforeWritingTheTarget(
        string op, string rhs, string source, ExecutionMode mode)
    {
        Assert.NotEmpty(op);
        Assert.NotEmpty(rhs);
        Assert.Equal("true 8 r\ntrue 8 air\ntrue 8 air\n", TestHarness.Run(source, mode));
    }
}
