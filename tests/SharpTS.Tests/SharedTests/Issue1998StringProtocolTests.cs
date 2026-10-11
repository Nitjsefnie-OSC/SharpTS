using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class Issue1998StringProtocolTests
{
    [Theory, ModeData]
    public void TypedStringProtocolsSkipPrimitivePrototypeGetters(ExecutionMode mode)
    {
        const string source = """
            for (const key of [Symbol.match, Symbol.matchAll, Symbol.search, Symbol.replace, Symbol.split]) {
                Object.defineProperty(Boolean.prototype, key, { get() { throw "unexpected protocol getter"; } });
            }
            console.log("atruebtruec".match(true as any)![0], "atruebtruec".search(true as any));
            console.log(Array.from("atruebtruec".matchAll(true as any)).length);
            console.log("atruebtruec".replace(true as any, "X"), "atruebtruec".replaceAll(true as any, "Y"));
            console.log("atruebtruec".split(true as any).join("|"));
            """;

        Assert.Equal("true 1\n2\naXbtruec aYbYc\na|b|c\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void MatchAndSearchSkipPrimitiveProtocolGetters(ExecutionMode mode)
    {
        const string source = """
            for (const prototype of [Boolean.prototype, Number.prototype, String.prototype, BigInt.prototype]) {
                for (const key of [Symbol.match, Symbol.search]) {
                    Object.defineProperty(prototype, key, { get() { throw "unexpected protocol getter"; } });
                }
            }
            const cases: any = [[true, "atruebtruec"], [1, "a1b1c"], [",", "a,b,c"], [1n, "a1b1c"]];
            for (const entry of cases) {
                const match: any = entry[1].match(entry[0]);
                console.log(match[0], match.index, match.input, entry[1].search(entry[0]));
            }
            """;

        Assert.Equal("true 1 atruebtruec 1\n1 1 a1b1c 1\n, 1 a,b,c 1\n1 1 a1b1c 1\n",
            TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void MatchAllSkipsPrimitiveMatchAndMatchAllGetters(ExecutionMode mode)
    {
        const string source = """
            for (const prototype of [Boolean.prototype, Number.prototype, String.prototype, BigInt.prototype]) {
                for (const key of [Symbol.match, Symbol.matchAll]) {
                    Object.defineProperty(prototype, key, { get() { throw "unexpected protocol getter"; } });
                }
            }
            const cases: any = [[true, "atruebtruec"], [1, "a1b1c"], [",", "a,b,c"], [1n, "a1b1c"]];
            for (const entry of cases) {
                const matches: any = Array.from(entry[1].matchAll(entry[0]));
                console.log(matches.length, matches[0][0], matches[0].index, matches[1].index, matches[0].input);
            }
            """;

        Assert.Equal("2 true 1 6 atruebtruec\n2 1 1 3 a1b1c\n2 , 1 3 a,b,c\n2 1 1 3 a1b1c\n",
            TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ReplaceReplaceAllAndSplitSkipPrimitiveProtocolGetters(ExecutionMode mode)
    {
        const string source = """
            for (const prototype of [Boolean.prototype, Number.prototype, String.prototype, BigInt.prototype]) {
                for (const key of [Symbol.match, Symbol.replace, Symbol.split]) {
                    Object.defineProperty(prototype, key, { get() { throw "unexpected protocol getter"; } });
                }
            }
            const cases: any = [[true, "atruebtruec"], [1, "a1b1c"], [",", "a,b,c"], [1n, "a1b1c"]];
            for (const entry of cases) {
                console.log(entry[1].replace(entry[0], "X"), entry[1].replaceAll(entry[0], "Y"), entry[1].split(entry[0]).join("|"));
            }
            """;

        Assert.Equal("aXbtruec aYbYc a|b|c\naXb1c aYbYc a|b|c\naXb,c aYbYc a|b|c\naXb1c aYbYc a|b|c\n",
            TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void BorrowedProtocolsSkipPrimitivePrototypeGetters(ExecutionMode mode)
    {
        const string source = """
            for (const key of [Symbol.match, Symbol.matchAll, Symbol.search, Symbol.replace, Symbol.split]) {
                Object.defineProperty(Boolean.prototype, key, { get() { throw "unexpected protocol getter"; } });
            }
            const p: any = String.prototype;
            console.log(p.match.call("atruebtruec", true)[0], p.search.apply("atruebtruec", [true]));
            console.log(Array.from(p.matchAll.bind("atruebtruec", true)()).length);
            console.log(p.replace.call("atruebtruec", true, "X"), p.replaceAll.apply("atruebtruec", [true, "Y"]));
            console.log(p.split.bind("atruebtruec", true)().join("|"));
            """;

        Assert.Equal("true 1\n2\naXbtruec aYbYc\na|b|c\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ObjectAndFunctionCandidatesStillDispatchProtocolMethods(ExecutionMode mode)
    {
        const string source = """
            const candidate: any = function() {};
            candidate.flags = "g";
            candidate[Symbol.match] = function(value: any) { console.log(this === candidate, value); return ["match"]; };
            candidate[Symbol.matchAll] = function(value: any) { console.log(this === candidate, value); return "all"; };
            candidate[Symbol.search] = function(value: any) { console.log(this === candidate, value); return 7; };
            candidate[Symbol.replace] = function(value: any, replacement: any) { console.log(this === candidate, value, replacement); return "replace"; };
            candidate[Symbol.split] = function(value: any, limit: any) { console.log(this === candidate, value, limit); return ["split"]; };
            const p: any = String.prototype;
            console.log(p.match.call("abc", candidate)[0], p.matchAll.call("abc", candidate), p.search.call("abc", candidate));
            console.log(p.replace.call("abc", candidate, "X"), p.replaceAll.call("abc", candidate, "Y"), p.split.call("abc", candidate, 2)[0]);
            """;

        Assert.Equal("true abc\ntrue abc\ntrue abc\nmatch all 7\ntrue abc X\ntrue abc Y\ntrue abc 2\nreplace replace split\n",
            TestHarness.Run(source, mode));
    }

    // The interpreter currently omits boxed wrapper prototype lookup for
    // symbol protocols. This preserves the compiler's existing Object path.
    [Theory, CompiledOnlyData]
    public void BoxedPrimitiveCandidatesStillObservePrototypeGetters(ExecutionMode mode)
    {
        const string source = """
            const box: any = new Number(1);
            let order = "";
            Object.defineProperty(Number.prototype, Symbol.search, {
                get() {
                    order += "get:" + (this === box) + ">";
                    return function(value: any) { order += "call:" + (this === box) + ">"; return value.length; };
                }
            });
            console.log("abc".search(box), order);
            console.log("a1b".search(1 as any), order);
            """;

        Assert.Equal("3 get:true>call:true>\n1 get:true>call:true>\n", TestHarness.Run(source, mode));
    }
}
