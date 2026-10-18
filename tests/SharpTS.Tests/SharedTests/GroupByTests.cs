using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

/// <summary>
/// Tests for Object.groupBy() and Map.groupBy(). Runs against both interpreter and compiler.
/// </summary>
public class GroupByTests
{
    public const string MapConstructorAliasSource = "const M:any=Map;const groups=M.groupBy([1,2,3],(x:number)=>x%2);console.log(groups.get(0).join(\",\"));\n";

    public static IEnumerable<object[]> MapAliasPrograms =>
    [
        ["constructor_alias", MapConstructorAliasSource],
        ["direct", "interface MapConstructor{groupBy(items:any,callback:any):any;}const groups=Map.groupBy([1,2,3],(x:number)=>x%2);console.log(groups.get(0).join(\",\"));\n"],
        ["detached", "interface MapConstructor{groupBy(items:any,callback:any):any;}const groupBy=Map.groupBy;const groups=groupBy([1,2,3],(x:number)=>x%2);console.log(groups.get(0).join(\",\"));\n"],
    ];

    [Theory, ModeData]
    public void MapGroupBy_ConstructorAlias_PreservesIssue1928Output(ExecutionMode mode)
    {
        Assert.Equal("2\n", TestHarness.Run(MapConstructorAliasSource, mode));
    }

    [Theory, ModeData]
    public void MapGroupBy_DirectAndDetachedControls(ExecutionMode mode)
    {
        foreach (var program in MapAliasPrograms.Skip(1))
            Assert.Equal("2\n", TestHarness.Run((string)program[1], mode));
    }

    [Theory, ModeData]
    public void MapGroupBy_AliasAndComputedLookup_PreserveFunctionIdentity(ExecutionMode mode)
    {
        var source = """
            const M: any = Map;
            const name = "groupBy";
            const groupBy = M[name];
            console.log(groupBy === Map.groupBy, groupBy === M.groupBy);
            console.log(groupBy.name, groupBy.length);
            console.log(groupBy([1,2,3], (x: number) => x%2).get(0).join(","));
            """;
        Assert.Equal("true true\ngroupBy 2\n2\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ObjectGroupBy_BasicGrouping(ExecutionMode mode)
    {
        var source = """
            const inventory = [
                { name: "asparagus", type: "vegetables" },
                { name: "bananas", type: "fruit" },
                { name: "goat", type: "meat" },
                { name: "cherries", type: "fruit" },
                { name: "fish", type: "meat" }
            ];
            const result: any = Object.groupBy(inventory, (item: any) => item.type);
            console.log(Object.keys(result).length);
            console.log(result.vegetables.length);
            console.log(result.fruit.length);
            console.log(result.meat.length);
            console.log(result.fruit[0].name);
            console.log(result.fruit[1].name);
            """;

        var output = TestHarness.Run(source, mode);
        Assert.Equal("3\n1\n2\n2\nbananas\ncherries\n", output);
    }

    [Theory, ModeData]
    public void ObjectGroupBy_EmptyArray(ExecutionMode mode)
    {
        var source = """
            const result: any = Object.groupBy([], (_: any) => "key");
            console.log(Object.keys(result).length);
            """;

        var output = TestHarness.Run(source, mode);
        Assert.Equal("0\n", output);
    }

    [Theory, ModeData]
    public void ObjectGroupBy_NumericKeys(ExecutionMode mode)
    {
        var source = """
            const nums = [1, 2, 3, 4, 5, 6];
            const result: any = Object.groupBy(nums, (n: any) => n % 2 === 0 ? "even" : "odd");
            console.log(result.odd.length);
            console.log(result.even.length);
            """;

        var output = TestHarness.Run(source, mode);
        Assert.Equal("3\n3\n", output);
    }

    [Theory, ModeData]
    public void ObjectGroupBy_CallbackReceivesIndex(ExecutionMode mode)
    {
        var source = """
            const arr = ["a", "b", "c", "d"];
            const result: any = Object.groupBy(arr, (_: any, i: number) => i < 2 ? "first" : "second");
            console.log(result.first.length);
            console.log(result.second.length);
            console.log(result.first[0]);
            console.log(result.second[0]);
            """;

        var output = TestHarness.Run(source, mode);
        Assert.Equal("2\n2\na\nc\n", output);
    }

    [Theory, ModeData]
    public void MapGroupBy_BasicGrouping(ExecutionMode mode)
    {
        var source = """
            const inventory = [
                { name: "asparagus", type: "vegetables" },
                { name: "bananas", type: "fruit" },
                { name: "cherries", type: "fruit" }
            ];
            const result = Map.groupBy(inventory, (item: any) => item.type);
            console.log(result.get("vegetables").length);
            console.log(result.get("fruit").length);
            console.log(result.get("fruit")[0].name);
            console.log(result.get("fruit")[1].name);
            """;

        var output = TestHarness.Run(source, mode);
        Assert.Equal("1\n2\nbananas\ncherries\n", output);
    }

    [Theory, ModeData]
    public void MapGroupBy_ConsumesGeneratorsInIterationOrder(ExecutionMode mode)
    {
        const string source = """
            function* values() { yield 1; yield 2; yield 3; }
            const map: any = Map, events: string[] = [];
            const result: any = map.groupBy(values(), (value: number, index: number) => {
                events.push(value + ':' + index); return value % 2;
            });
            console.log(result.size, result.get(1).join(','), result.get(0).join(','), events.join('|'));
            """;
        Assert.Equal("2 1,3 2 1:0|2:1|3:2\n", TestHarness.Run(source, mode));
    }

    // #2572 tracks compiled Set iterable admission.
    [Theory, InterpretedOnlyData]
    public void MapGroupBy_ConsumesSetsInIterationOrder(ExecutionMode mode)
    {
        const string source = """
            const map: any = Map;
            const result: any = map.groupBy(new Set([2, 2, 3]), (value: number) => value % 2);
            console.log(result.size, result.get(0).join(','), result.get(1).join(','));
            """;
        Assert.Equal("2 2 3\n", TestHarness.Run(source, mode));
    }

    [Fact]
    public void MapGroupBy_ClosesTheActualIteratorAndPreservesTheCallbackThrow()
    {
        // #2309 tracks the distinct compiled cleanup precedence failure.
        const string source = """
            const marker = {}, cleanup = {}, events: string[] = [];
            const iterator: any = {
                next() { events.push('next'); return { done: false, value: 1 }; },
                return() { events.push('return'); throw cleanup; }
            };
            const source: any = {
                [Symbol.iterator]() { events.push('acquire'); return iterator; },
                return() { events.push('wrong'); return {}; }
            };
            const map: any = Map;
            try { map.groupBy(source, () => { events.push('callback'); throw marker; }); }
            catch (error) { console.log(error === marker, events.join('|')); }
            """;
        Assert.Equal("true acquire|next|callback|return\n", TestHarness.RunInterpreted(source));
    }

    [Theory, ModeData]
    public void MapGroupBy_ValidatesCallbackBeforeAcquiringTheIterator(ExecutionMode mode)
    {
        const string source = """
            let reads = 0;
            const source: any = { get [Symbol.iterator]() { reads++; throw 1; } };
            const map: any = Map;
            try { map.groupBy(source, null); } catch (error) { console.log(error instanceof TypeError, reads); }
            function key(value: number) { 'use strict'; console.log(this === undefined); return value; }
            console.log(map.groupBy([1], key).size);
            """;
        Assert.Equal("true 0\ntrue\n1\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void MapGroupBy_EmptyArray(ExecutionMode mode)
    {
        var source = """
            const result = Map.groupBy([], (_: any) => "key");
            console.log(result.size);
            """;

        var output = TestHarness.Run(source, mode);
        Assert.Equal("0\n", output);
    }

    [Theory, ModeData]
    public void MapGroupBy_NullUndefinedKeys(ExecutionMode mode)
    {
        var source = """
            const items = [1, 2, 3];
            const result = Map.groupBy(items, (n: any) => n > 2 ? "big" : undefined);
            console.log(result.size);
            console.log(result.get(undefined).length);
            console.log(result.get(undefined)[0]);
            console.log(result.get(undefined)[1]);
            console.log(result.get("big").length);
            console.log(result.get("big")[0]);
            """;

        var output = TestHarness.Run(source, mode);
        Assert.Equal("2\n2\n1\n2\n1\n3\n", output);
    }

    [Theory, ModeData]
    public void MapGroupBy_MultipleItemsPerGroup(ExecutionMode mode)
    {
        var source = """
            const nums = [1, 2, 3, 4, 5, 6];
            const result = Map.groupBy(nums, (n: any) => { return n % 2 === 0 ? "even" : "odd"; });
            const odd = result.get("odd");
            const even = result.get("even");
            console.log(odd.length);
            console.log(even.length);
            console.log(odd[0]);
            console.log(even[0]);
            """;

        var output = TestHarness.Run(source, mode);
        Assert.Equal("3\n3\n1\n2\n", output);
    }
}
