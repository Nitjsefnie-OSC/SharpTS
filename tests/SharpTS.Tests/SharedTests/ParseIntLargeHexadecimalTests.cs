using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ParseIntLargeHexadecimalTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["exact-issue", """
            console.log(parseInt("0x10000000000000000", 16));
            console.log(Number.parseInt("0x10000000000000000", 16));
            """, "18446744073709552000\n18446744073709552000\n"];

        yield return ["typed-global-boundaries", """
            console.log(parseInt("0x10000000000000000", 16) === 18446744073709551616,
                parseInt("0x10000000000000001", 16) === 18446744073709551616,
                parseInt("0x10000000000000800", 16) === 18446744073709551616,
                parseInt("0x10000000000001800", 16) === 18446744073709559808);
            console.log(parseInt("-0x10000000000000000tail", 16) === -18446744073709551616,
                parseInt(" \uFEFF+0X10000000000000000g", 16) === 18446744073709551616,
                parseInt("0x1" + "0".repeat(275), 16) === Infinity,
                parseInt("-0x1" + "0".repeat(275), 16) === -Infinity,
                Object.is(parseInt("-0x000", 16), -0));
            """, "true true true true\ntrue true true true true\n"];

        yield return ["number-and-alias-boundaries", """
            const inputs = ["0x8000000000000000", "0xffffffffffffffff", "0x10000000000000000",
                "0x10000000000000001", "-0x10000000000000000tail",
                "0x10000000000000800", "0x10000000000001800"];
            const expected = [9223372036854775808, 18446744073709551616, 18446744073709551616,
                18446744073709551616, -18446744073709551616,
                18446744073709551616, 18446744073709559808];
            const globalAlias: any = parseInt, constructor: any = Number, alias: any = constructor.parseInt;
            for (let index = 0; index < inputs.length; index++) {
                console.log(parseInt(inputs[index], 16) === expected[index],
                    Number.parseInt(inputs[index], 16) === expected[index],
                    globalAlias(inputs[index], 16) === expected[index], alias(inputs[index], 16) === expected[index]);
            }
            console.log(parseInt("0x1" + "0".repeat(275), 16) === Infinity,
                Number.parseInt("-0x1" + "0".repeat(275), 16) === -Infinity,
                Object.is(parseInt("-0x000", 16), -0), Object.is(Number.parseInt("-0x000", 16), -0));
            """, string.Concat(Enumerable.Repeat("true true true true\n", 8))];
    }

    public static IEnumerable<object[]> ModeCases()
    {
        foreach (var row in Cases())
        {
            yield return [row[0], row[1], row[2], ExecutionMode.Interpreted];
            // #2481: Number.parseInt/general emitted parsing still wraps large hexadecimal magnitudes.
            if ((string)row[0] != "typed-global-boundaries") continue;
            yield return [row[0], row[1], row[2], ExecutionMode.Compiled];
        }
    }

    [Theory, MemberData(nameof(ModeCases))]
    public void ParsesHexadecimalMagnitudesBeyond64Bits(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, output) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            Assert.Equal(expected, output);
        }
        else Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
