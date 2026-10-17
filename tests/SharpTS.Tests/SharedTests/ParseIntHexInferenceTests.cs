using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ParseIntHexInferenceTests
{
    public const string IssueSource = """
        console.log(parseInt("0x10"), Number.parseInt("0x10"));
        """;

    [Theory, ModeData]
    public void OmittedRadixInfersHexadecimal(ExecutionMode mode)
        => Assert.Equal("16 16\n", TestHarness.Run(IssueSource, mode));

    [Theory, ModeData]
    public void InferenceAgreesAcrossCallForms(ExecutionMode mode)
    {
        const string source = """
            const global: any = parseInt, constructor: any = Number, alias: any = constructor.parseInt;
            for (const input of ["0x10", "0Xff", "-0x10", " +0X20tail", "\uFEFF0x10", "077", "0b10"]) {
                console.log(parseInt(input), Number.parseInt(input), global(input),
                    constructor.parseInt(input), alias.call({}, input));
            }
            console.log(Number.isNaN(parseInt("0x")), Number.isNaN(Number.parseInt("0x")));
            console.log(Object.is(parseInt("-0x0"), -0), Object.is(Number.parseInt("-0x0"), -0));
            """;
        Assert.Equal("16 16 16 16 16\n255 255 255 255 255\n-16 -16 -16 -16 -16\n32 32 32 32 32\n"
            + "16 16 16 16 16\n77 77 77 77 77\n0 0 0 0 0\ntrue true\ntrue true\n",
            TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void DefaultRadixValuesAndExplicitRadicesRetainTheirMeaning(ExecutionMode mode)
    {
        const string source = """
            const global: any = parseInt, constructor: any = Number;
            for (const radix of [undefined, 0, NaN]) {
                console.log(global("0x10", radix), constructor.parseInt("0x10", radix));
            }
            console.log(parseInt("0x10", 10), Number.parseInt("0x10", 10),
                parseInt("0x10", 16), Number.parseInt("0x10", 16),
                parseInt("\uFEFF0x10", 16), Number.parseInt("\uFEFF0x10", 16));
            console.log(Number.isNaN(global("0x10", 1)), Number.isNaN(constructor.parseInt("0x10", 37)));
            """;
        Assert.Equal("16 16\n16 16\n16 16\n0 0 16 16 16 16\ntrue true\n", TestHarness.Run(source, mode));
    }

    [Fact]
    public void InterpretedStringRadicesKeepTheirMeaning()
    {
        // Compiled non-numeric radix coercion is tracked separately in #2469.
        Assert.Equal("0 0\n16 16\n", TestHarness.RunInterpreted("""
            const global: any = parseInt, constructor: any = Number;
            console.log(global("0x10", "10"), constructor.parseInt("0x10", "10"));
            console.log(global("10", "16"), constructor.parseInt("10", "16"));
            """));
    }

    [Theory, ModeData]
    public void NextLineIsNotWhitespaceEvenAfterABom(ExecutionMode mode)
    {
        const string source = """
            const global: any = parseInt, constructor: any = Number;
            for (const input of ["\u00850x10", "\uFEFF\u00850x10", "\u0085\uFEFF0x10"]) {
                console.log(Number.isNaN(parseInt(input)), Number.isNaN(Number.parseInt(input)),
                    Number.isNaN(global(input)), Number.isNaN(constructor.parseInt(input)),
                    Number.isNaN(parseInt(input, 16)), Number.isNaN(constructor.parseInt(input, 16)));
            }
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat("true true true true true true\n", 3)),
            TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void HexadecimalMagnitudeIsUnsignedBeforeApplyingSign(ExecutionMode mode)
    {
        const string source = """
            console.log(parseInt("-0x8000000000000000", 16) === -9223372036854775808,
                parseInt("0x8000000000000000", 16) === 9223372036854775808,
                parseInt("0xffffffffffffffff", 16) === 18446744073709551616,
                Object.is(parseInt("-0x0", 16), -0));
            """;
        Assert.Equal("true true true true\n", TestHarness.Run(source, mode));
    }

    [Fact]
    public void InterpretedNumberAliasesPreserveLargeHexadecimalSigns()
    {
        // Compiled Number aliases retain their general-parser magnitude limitation (#2481).
        Assert.Equal("true true true\n", TestHarness.RunInterpreted("""
            const constructor: any = Number, alias: any = constructor.parseInt;
            console.log(Number.parseInt("-0x8000000000000000", 16) === -9223372036854775808,
                constructor.parseInt("0x8000000000000000", 16) === 9223372036854775808,
                alias("0xffffffffffffffff", 16) === 18446744073709551616);
            """));
    }
}
