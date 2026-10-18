using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class MixedLiteralUnionReturnTests
{
    private const string ObjectMethodExact = """
        const object = {
            value(which: number) {
                if (which === 0) return 1;
                if (which === 1) return 0;
                if (which === 2) return "yes";
                return false;
            }
        };
        console.log(object.value(0), object.value(1), object.value(2), object.value(3));
        """;

    private const string ObjectMethodClassification = """
        const object = {
            value(which: number) {
                if (which === 0) return 1;
                if (which === 1) return 0;
                if (which === 2) return "yes";
                return false;
            }
        };
        const nonzero = object.value(0);
        const zero = object.value(1);
        const text = object.value(2);
        const negative = object.value(3);
        console.log(typeof nonzero, nonzero === 1, !!nonzero);
        console.log(typeof zero, zero === 0, !!zero);
        console.log(typeof text, text === "yes", !!text);
        console.log(typeof negative, negative === false, !!negative);
        """;

    private const string OrdinaryFunctionControl = """
        function value(which: number) {
            if (which === 0) return 1;
            if (which === 1) return 0;
            if (which === 2) return "yes";
            return false;
        }
        console.log(value(0), value(1), value(2), value(3));
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["object-method-exact", ObjectMethodExact, "1 0 yes false\n"];
        yield return ["object-method-classification", ObjectMethodClassification,
            "number true true\nnumber true false\nstring true true\nboolean true false\n"];
        yield return ["ordinary-function-control", OrdinaryFunctionControl, "1 0 yes false\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void InferredMixedLiteralsPreservePrimitiveValues(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
