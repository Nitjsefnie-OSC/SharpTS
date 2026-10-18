using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ObjectArithmeticCoercionTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var (op, expected) in new[]
        {
            ("*", "16"), ("/", "4"), ("%", "0"), ("**", "64"), ("-", "6")
        })
        {
            yield return [$"valueof-and-evaluation-order-{op}", $$"""
                let trace = "";
                const left: any = {
                    valueOf() { trace += "l"; return 8; },
                    toString() { throw "unexpected left toString"; }
                };
                const right: any = {
                    valueOf() { trace += "r"; return 2; },
                    toString() { throw "unexpected right toString"; }
                };
                function lhs(): any { trace += "L"; return left; }
                function rhs(): any { trace += "R"; return right; }
                console.log(lhs() {{op}} rhs(), trace);
                """, $"{expected} LRlr\n"];

            yield return [$"tostring-fallback-{op}", $$"""
                let trace = "";
                const left: any = {
                    valueOf() { trace += "lv;"; return {}; },
                    toString() { trace += "ls;"; return "8"; }
                };
                const right: any = {
                    valueOf() { trace += "rv;"; return {}; },
                    toString() { trace += "rs;"; return "2"; }
                };
                console.log(left {{op}} right, trace);
                """, $"{expected} lv;ls;rv;rs;\n"];

            yield return [$"symbol-toprimitive-number-hint-{op}", $$"""
                let trace = "";
                const left: any = {
                    [Symbol.toPrimitive](hint: string) { trace += "l:" + hint + ";"; return 8; },
                    valueOf() { throw "unexpected left valueOf"; }
                };
                const right: any = {
                    [Symbol.toPrimitive](hint: string) { trace += "r:" + hint + ";"; return 2; },
                    valueOf() { throw "unexpected right valueOf"; }
                };
                console.log(left {{op}} right, trace);
                """, $"{expected} l:number;r:number;\n"];

            yield return [$"throwing-left-skips-right-conversion-{op}", $$"""
                let trace = "";
                const marker: any = {};
                const left: any = { valueOf() { trace += "l"; throw marker; } };
                const right: any = { valueOf() { trace += "r"; return 2; } };
                function lhs(): any { trace += "L"; return left; }
                function rhs(): any { trace += "R"; return right; }
                try { console.log(lhs() {{op}} rhs()); }
                catch (error) { console.log(error === marker, trace); }
                """, "true LRl\n"];

            yield return [$"object-results-throw-typeerror-{op}", $$"""
                let trace = "";
                const right: any = { valueOf() { trace += "r"; return 2; } };
                const ordinary: any = {
                    valueOf() { trace += "v"; return {}; },
                    toString(): any { trace += "s"; return {}; }
                };
                try { console.log(ordinary {{op}} right); }
                catch (error) { console.log(error instanceof TypeError, trace); }
                trace = "";
                const exotic: any = {
                    [Symbol.toPrimitive](hint: string): any { trace += hint; return {}; },
                    valueOf() { trace += "v"; return 8; }
                };
                try { console.log(exotic {{op}} right); }
                catch (error) { console.log(error instanceof TypeError, trace); }
                """, "true vs\ntrue number\n"];

            yield return [$"wrappers-and-nan-controls-{op}", $$"""
                const left: any = new Number(8);
                const right: any = new String("2");
                console.log(left {{op}} right);
                const absent: any = undefined;
                const plain: any = {};
                const invalid: any = { valueOf() { return "not numeric"; } };
                console.log(Number.isNaN(absent {{op}} 2));
                console.log(Number.isNaN(plain {{op}} 2));
                console.log(Number.isNaN(invalid {{op}} 2));
                """, $"{expected}\ntrue\ntrue\ntrue\n"];
        }

        yield return ["boolean-wrappers", """
            const truthy: any = new Boolean(true);
            const falsy: any = new Boolean(false);
            const amount: any = 2;
            console.log(truthy * amount, truthy / amount, truthy % amount, truthy ** amount, truthy - amount);
            console.log(falsy * amount, falsy / amount, falsy % amount, falsy ** amount, falsy - amount);
            """, "2 0.5 1 1 -1\n0 0 0 0 -2\n"];
    }

    public static IEnumerable<object[]> BoxedHookCases()
    {
        foreach (var (wrapper, argument, product, bitwise) in new[]
        {
            ("Number", "8", "16", "10"),
            ("String", "\"8\"", "16", "10"),
            ("Boolean", "true", "2", "3")
        })
        {
            foreach (string nullish in new[] { "undefined", "null" })
            {
                yield return [$"boxed-{wrapper}-nullish-hook-read-once-{nullish}", $$"""
                    const state = { reads: 0, receiverMatches: true };
                    const box: any = new {{wrapper}}({{argument}});
                    Object.defineProperty(box, Symbol.toPrimitive, {
                        get() {
                            state.reads++;
                            state.receiverMatches = state.receiverMatches && this === box;
                            if (state.reads > 1) throw "duplicate Symbol.toPrimitive lookup";
                            return {{nullish}};
                        }
                    });
                    console.log(box * 2, state.reads, state.receiverMatches);
                    state.reads = 0;
                    console.log(box | 2, state.reads, state.receiverMatches);
                    """, $"{product} 1 true\n{bitwise} 1 true\n"];
            }
        }
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void NumericArithmeticConvertsObjectOperandsInOrder(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    // Compiled numeric wrapper coercion also performs a duplicate hook lookup (#2516).
    [Theory, MemberData(nameof(BoxedHookCases))]
    public void InterpreterBoxedNumericCoercionRetrievesNullishPrimitiveHookOnce(
        string name, string source, string expected)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, ExecutionMode.Interpreted));
    }
}
