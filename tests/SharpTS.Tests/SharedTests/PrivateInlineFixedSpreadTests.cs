using SharpTS.Tests.Infrastructure;
using SharpTS.TypeSystem;
using SharpTS.TypeSystem.Exceptions;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class PrivateInlineFixedSpreadTests
{
    public const string StaticSource = """
        class C {
            static #pair(first: number, second?: string) {
                return [first, second === undefined ? "missing" : second, arguments.length].join("|");
            }
            static #array(first: number[], second: string) {
                return [first.join(","), second, arguments.length].join("|");
            }
            static #field: (first: number, second?: string) => string =
                (first: number, second?: string | undefined) => first + "|" + (second === undefined ? "missing" : second);
            static read() {
                console.log(this.#pair(...[1, "two"]));
                console.log(this.#pair(...(([2, "grouped"]))));
                console.log(this.#pair(...[3]));
                console.log(this.#pair(...[4], ...["separate"]));
                console.log(this.#pair(...[5, ,]));
                console.log(this.#pair(6, ...["leading"]));
                console.log(this.#pair(...[7], "trailing"));
                console.log(this.#array(...[[8, 9], "array"]));
                console.log(this.#field(...[10, "field"]));
                console.log(this.#field(...(([11, "grouped-field"]))));
                console.log(this.#field(...[12, ,]));
            }
        }
        C.read();
        """;

    public const string StaticExpected = "1|two|2\n2|grouped|2\n3|missing|1\n4|separate|2\n5|missing|2\n"
        + "6|leading|2\n7|trailing|2\n8,9|array|2\n10|field\n11|grouped-field\n12|missing\n";

    [Theory, ModeData]
    public void StaticPrivateInlineSpreadsPreservePositionsHolesAndNestedArrayValues(ExecutionMode mode)
    {
        Assert.Equal(StaticExpected, TestHarness.Run(StaticSource, mode));
    }

    // Direct instance private spread emission is tracked separately by #2153.
    [Theory]
    [InlineData("first: number, second?: string", "...[1, 'two']")]
    [InlineData("first: number, second?: string", "...[1]")]
    [InlineData("first: number, second?: string", "...(([1, 'two']))")]
    [InlineData("first: number, second?: string", "...[1], ...['two']")]
    [InlineData("first: number, second?: string", "...[1, ,]")]
    [InlineData("first: number, second?: string", "1, ...['two']")]
    [InlineData("first: number, second?: string", "...[1], 'two'")]
    [InlineData("first: number[], second?: string", "...[[1, 2], 'two']")]
    public void StaticAndInstancePrivateMethodsAcceptFixedInlinePositions(string parameters, string arguments)
    {
        foreach (var modifier in new[] { "", "static " })
        {
            var source = MethodSource(modifier, parameters, arguments);
            new TypeChecker().Check(TestHarness.ParseOrThrow(source));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TypedCallablePrivateFieldsAcceptFixedInlinePositions(bool isStatic)
    {
        string modifier = isStatic ? "static " : "";
        var source = $$"""
            class C {
                {{modifier}}#field: (first: number, second?: string) => string =
                    (first: number, second?: string) => first + ":" + second;
                {{modifier}}read() { this.#field(...[1, "two"]); }
            }
            """;
        new TypeChecker().Check(TestHarness.ParseOrThrow(source));
    }

    [Theory]
    [InlineData("first: number, second?: string", "...[]", "TS2554")]
    [InlineData("first: number, second?: string", "...[1, 'two', 3]", "TS2554")]
    [InlineData("first: number, second?: string", "...['bad', 'two']", "TS2345")]
    [InlineData("first: number, second?: string", "...[1, 2]", "TS2345")]
    [InlineData("first: number, second?: string", "...[, 'two']", "TS2345")]
    [InlineData("first: number, second: string", "...[1, ,]", "TS2345")]
    [InlineData("first: number[], second?: string", "...[[1, 'bad'], 'two']", "TS2345")]
    [InlineData("first: number, second?: string", "...[...[1], 'two']", "TS2556")]
    [InlineData("first: number, second?: string", "...([1, 'two'] as (number | string)[])", "TS2556")]
    public void StaticAndInstancePrivateInlineSpreadsRetainArityElementAndIterableChecks(
        string parameters, string arguments, string code)
    {
        foreach (var modifier in new[] { "", "static " })
        {
            var source = MethodSource(modifier, parameters, arguments);
            var error = Assert.ThrowsAny<TypeCheckException>(() =>
                new TypeChecker().Check(TestHarness.ParseOrThrow(source)));
            Assert.Equal(code, error.Diagnostic.TsCode);
        }
    }

    private static string MethodSource(string modifier, string parameters, string arguments) => $$"""
        class C {
            {{modifier}}#m({{parameters}}) {}
            {{modifier}}read() { this.#m({{arguments}}); }
        }
        """;
}
