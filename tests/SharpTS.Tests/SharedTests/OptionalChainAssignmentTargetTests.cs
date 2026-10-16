using SharpTS.Parsing;
using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class OptionalChainAssignmentTargetTests
{
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
