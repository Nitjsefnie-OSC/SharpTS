using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class PrimitiveLooseEqualityTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["primitives", """
            function compare(left: any, right: any) {
                console.log(left == right, right == left, left != right, right != left,
                    left === right, right === left);
            }
            compare(0, "0");
            compare(0, false);
            compare(1, true);
            compare(false, "");
            compare(false, "  \t\n");
            compare(true, "01");
            compare(false, "false");
            compare(0, "not a number");
            compare(NaN, "NaN");
            compare(NaN, NaN);
            compare(-0, "-0");
            compare(Infinity, "Infinity");
            compare(16, "0x10");
            compare(3, "0b11");
            compare(100, "1e2");
            compare("01", "1");
            compare("", "0");
            compare(null, false);
            compare(undefined, false);
            compare(null, undefined);
            const symbol: any = Symbol("zero");
            compare(symbol, 0);
            compare(symbol, false);
            compare(symbol, symbol);
            compare(new Number(0), false);
            compare(new String("1"), 1);
            """, "true true false false false false\ntrue true false false false false\ntrue true false false false false\ntrue true false false false false\ntrue true false false false false\ntrue true false false false false\nfalse false true true false false\nfalse false true true false false\nfalse false true true false false\nfalse false true true false false\ntrue true false false false false\ntrue true false false false false\ntrue true false false false false\ntrue true false false false false\ntrue true false false false false\nfalse false true true false false\nfalse false true true false false\nfalse false true true false false\nfalse false true true false false\ntrue true false false false false\nfalse false true true false false\nfalse false true true false false\ntrue true false false true true\ntrue true false false false false\ntrue true false false false false\n"];
        yield return ["objects", """
            function equal(left: any, right: any): boolean { return left == right; }
            function different(left: any, right: any): boolean { return left != right; }
            let trace = "";
            const numberValue: any = { valueOf() { trace += "N"; return "1"; } };
            const boolValue: any = { valueOf() { trace += "B"; return false; } };
            const nullValue: any = { valueOf() { trace += "X"; return null; } };
            console.log(equal(numberValue, 1), different(1, numberValue), trace);
            trace = "";
            console.log(equal(boolValue, ""), different("0", boolValue), trace);
            trace = "";
            console.log(equal(nullValue, 0), equal(0, nullValue), trace);
            trace = "";
            console.log(equal(numberValue, numberValue), equal(numberValue, boolValue), trace);
            const nan: any = new Number(NaN);
            console.log(equal(nan, NaN), equal(NaN, nan));
            """, "true false NN\ntrue false BB\nfalse false XX\ntrue false \nfalse false\n"];
        yield return ["resumable", """
            async function compare(left: any, right: any): Promise<boolean> {
                return left == await Promise.resolve(right);
            }
            function* generator(left: any): any {
                const equal = left == (yield "right");
                return equal;
            }
            async function run() {
                console.log(await compare(false, ""), await compare("1", true), await compare("01", "1"));
                const first: any = generator("0"), second: any = generator(false);
                console.log(first.next().value, first.next(false).value);
                console.log(second.next().value, second.next(0).value);
            }
            run().catch((error: any) => console.log("unexpected", error));
            """, "true true false\nright true\nright true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void PrimitiveCoercionPreservesLooseEquality(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
