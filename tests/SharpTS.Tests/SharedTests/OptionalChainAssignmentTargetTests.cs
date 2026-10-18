using SharpTS.Parsing;
using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class OptionalChainAssignmentTargetTests
{
    public static IEnumerable<object[]> OptionalUpdateTargets()
    {
        string[] targets = ["holder?.item", "holder?.[key]", "holder?.nested.item",
            "holder?.nested[key]", "holder?.().item", "holder?.method().item",
            "holder.method?.().item", "holder?.nested!.item"];
        foreach (string target in targets)
            foreach (string op in new[] { "++", "--" })
            {
                yield return [op + target + ";"];
                yield return [target + op + ";"];
            }
    }

    [Theory]
    [MemberData(nameof(OptionalUpdateTargets))]
    public void UpdatesCannotWriteThroughOptionalChains(string update)
    {
        var parser = new Parser(new Lexer(update).ScanTokens());
        var result = parser.Parse();
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("optional", StringComparison.OrdinalIgnoreCase));
    }

    [Theory, ModeData]
    public void UpdatesAllowOptionalReadsOutsideTheReceiverChain(ExecutionMode mode)
    {
        const string source = """
            const holder: any = { nested: { item: 1 }, items: [1] };
            const optional: any = { index: 0, value: 9 };
            function make(value: any) { return holder.nested; }
            console.log(holder.items[optional?.index]++);
            console.log(++make(optional?.value).item);
            console.log((holder?.nested).item--);
            console.log(--(holder?.items)[0]);
            console.log(holder.nested.item, holder.items[0]);
            """;
        Assert.Equal("1\n2\n2\n1\n1 1\n", TestHarness.Run(source, mode));
    }

    [Theory]
    [InlineData("[holder?.item] = [7];")]
    [InlineData("[holder?.[key]] = [7];")]
    [InlineData("[holder?.nested.item] = [7];")]
    [InlineData("[holder?.nested[key]] = [7];")]
    [InlineData("[holder?.().item] = [7];")]
    [InlineData("[holder?.method().item] = [7];")]
    [InlineData("[holder.method?.().item] = [7];")]
    [InlineData("[(holder?.item)] = [7];")]
    [InlineData("[(holder?.nested.item)] = [7];")]
    [InlineData("[holder?.item = 7] = [];")]
    [InlineData("[holder?.[key] = 7] = [];")]
    [InlineData("[holder?.nested.item = optional?.value] = [];")]
    [InlineData("({value: holder?.item} = {value: 7});")]
    [InlineData("({value: holder?.[key] = 7} = {});")]
    [InlineData("[[holder?.item] = []] = [];")]
    [InlineData("[...holder?.items] = [7];")]
    [InlineData("({ ...holder?.items } = {});")]
    [InlineData("holder?.item = 7;")]
    [InlineData("holder?.[key] += 1;")]
    [InlineData("holder?.item ??= 1;")]
    public void DestructuringCannotWriteThroughOptionalChains(string assignment)
    {
        var source = "const holder: any = {}; const key = 'item'; " + assignment;
        var parser = new Parser(new Lexer(source).ScanTokens());
        Assert.False(parser.Parse().IsSuccess);
    }

    [Theory, ModeData]
    public void OptionalReadsAndGroupedReceiverBoundariesRemainValid(ExecutionMode mode)
    {
        const string source = """
            const holder: any = { nested: { item: 0 }, item: 0 };
            const optional: any = { key: "item", value: 9 };
            holder.method = () => holder.nested;
            function make(value: any) { return holder.nested; }
            [holder[optional?.key]] = [7];
            [(holder?.method()).item] = [7];
            [(holder.method?.()).item] = [8];
            [make(optional?.value).item] = [7];
            [(holder?.nested).item] = [8];
            [(holder?.nested)["item"] = optional?.value] = [];
            ({ value: holder.item = optional?.value } = {});
            ({ [optional?.key]: holder.item = optional?.value } = {});
            console.log(holder.item, holder.nested.item, optional?.value);
            """;
        Assert.Equal("9 9 9\n", TestHarness.Run(source, mode));
    }
}
