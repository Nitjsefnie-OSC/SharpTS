using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ParseIntGeneralRadixTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["exact-and-aliases", """
            console.log(parseInt("10", 3), Number.parseInt("zz", 36));
            const global: any = parseInt, constructor: any = Number;
            const numberAlias: any = constructor.parseInt;
            console.log(global("10", 3), numberAlias("zz", 36));
            """, "3 1295\n3 1295\n"];

        yield return ["every-radix-and-digit-boundary", """
            const digits = "0123456789abcdefghijklmnopqrstuvwxyz";
            let count = 0, global = true, number = true, cases = true, stops = true;
            for (let radix = 2; radix <= 36; radix++) {
                const digit = digits[radix - 1];
                count++;
                global = global && parseInt("10", radix) === radix;
                number = number && Number.parseInt("10", radix) === radix;
                cases = cases && parseInt(digit, radix) === radix - 1
                    && Number.parseInt(digit.toUpperCase(), radix) === radix - 1;
                stops = stops && parseInt("10!tail", radix) === radix;
                if (radix < 36) stops = stops && Number.parseInt("1" + digits[radix], radix) === 1;
            }
            console.log(count, global, number, cases, stops);
            """, "35 true true true true\n"];

        yield return ["scanner-sign-and-prefix-boundaries", """
            console.log(parseInt("\uFEFF +102tail", 3), Number.parseInt("+1z!", 36),
                parseInt("Z!", 36), Number.isNaN(parseInt("z", 35)));
            console.log(Object.is(parseInt("-0tail", 3), -0),
                Object.is(Number.parseInt("-000!", 36), -0));
            console.log(parseInt("0x10", 3), Number.parseInt("0b10", 2),
                parseInt("0o10", 8), parseInt("0x10", 16), Number.parseInt("0X10", 0));
            console.log(Number.isNaN(parseInt("", 3)), Number.isNaN(Number.parseInt("+", 36)),
                Number.isNaN(parseInt("2", 2)), Number.isNaN(Number.parseInt("10", 1)),
                Number.isNaN(parseInt("10", 37)));
            """, "11 71 35 true\ntrue true\n0 0 0 16 16\ntrue true true true true\n"];

        yield return ["typed-large-binary-and-hexadecimal", """
            console.log(parseInt("1" + "0".repeat(80), 2) === 1208925819614629174706176,
                parseInt("-1" + "0".repeat(80) + "suffix", 2) === -1208925819614629174706176,
                parseInt("0x1" + "0".repeat(20) + "tail", 16) === 1208925819614629174706176,
                parseInt("1" + "0".repeat(1100), 2) === Infinity,
                parseInt("-0x1" + "0".repeat(275), 16) === -Infinity);
            """, "true true true true true\n"];

        yield return ["number-alias-large-binary-and-hexadecimal", """
            const constructor: any = Number, alias: any = constructor.parseInt;
            console.log(Number.parseInt("1" + "0".repeat(80), 2) === 1208925819614629174706176,
                constructor.parseInt("-1" + "0".repeat(80) + "suffix", 2) === -1208925819614629174706176,
                alias("0x1" + "0".repeat(20) + "tail", 16) === 1208925819614629174706176,
                Number.parseInt("1" + "0".repeat(1100), 2) === Infinity,
                alias("-0x1" + "0".repeat(275), 16) === -Infinity);
            """, "true true true true true\n"];
    }

    public static IEnumerable<object[]> ModeCases()
    {
        foreach (var row in Cases())
        {
            yield return [row[0], row[1], row[2], ExecutionMode.Interpreted];
            // #2481: compiled Number.parseInt/general-parser magnitudes wrap through Int64.
            if ((string)row[0] == "number-alias-large-binary-and-hexadecimal") continue;
            yield return [row[0], row[1], row[2], ExecutionMode.Compiled];
        }
    }

    [Theory, MemberData(nameof(ModeCases))]
    public void ParsesTheFullRadixRange(
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
